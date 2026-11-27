using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
 
public struct StatusEntry : INetworkSerializeByMemcpy, IEquatable<StatusEntry>
{
    public int EffectId;  
    public int Stacks;
    public double EndTime; 
    public ulong Source;    
 
    public bool Equals(StatusEntry other) =>
        EffectId == other.EffectId && Stacks == other.Stacks &&
        EndTime == other.EndTime && Source == other.Source;
}
public sealed class StatusEffectReceiver : NetworkBehaviour
{
    public static StatusEffectReceiver Local { get; private set; }
 
    [Tooltip("Dazed: sways per second.")]
    [SerializeField] private float _swaySpeed = 0.6f;
 
    private readonly NetworkList<StatusEntry> _entries = new NetworkList<StatusEntry>();
    private readonly List<double> _nextTick = new List<double>();
 
    private FirstPersonController _player; 
    private StatusModifiers _mods = StatusModifiers.None;
    private int _modsFrame = -1;
 
    private static StatusEffectRegistry Registry => StatusEffectRegistry.Instance;
    private double Now => NetworkManager.ServerTime.Time;
 
    public StatusModifiers Mods
    {
        get
        {
            if (_modsFrame != Time.frameCount)
            {
                _modsFrame = Time.frameCount;
                _mods = Collect();
            }
            return _mods;
        }
    }
 
    private void Awake()
    {
        _player = GetComponent<FirstPersonController>();
    }
 
    public override void OnNetworkSpawn()
    {
        if (IsOwner && _player != null) Local = this;
        _entries.OnListChanged += OnEntriesChanged;
    }
 
    public override void OnNetworkDespawn()
    {
        _entries.OnListChanged -= OnEntriesChanged;
        if (Local != this) return;
 
        Local = null;
        _player.SpeedMultiplier = 1f;
        _player.MovementReversed = false;
        _player.CameraSway = Vector3.zero;
        AudioListener.volume = 1f;
    }
 
    // ----- queries (every machine) -----------------------------------------------
 
    public int Count => IsSpawned ? _entries.Count : 0;
    public StatusEntry EntryAt(int i) => _entries[i];
 
    public float RemainingOf(StatusEntry entry) =>
        entry.EndTime < 0.0 ? float.PositiveInfinity : Mathf.Max(0f, (float)(entry.EndTime - Now));
 
    public bool Has(StatusEffect effect) => IndexOf(IdOf(effect)) >= 0;
 
    public int StacksOf(StatusEffect effect)
    {
        int i = IndexOf(IdOf(effect));
        return i >= 0 ? _entries[i].Stacks : 0;
    }
 
    // ----- applying and removing (server) ----------------------------------------
 
    public void Apply(StatusEffect effect, ulong source = PlayerHealth.EnvironmentAttacker)
    {
        if (!IsServer || !IsSpawned || effect == null) return;
 
        int id = IdOf(effect);
        if (id < 0)
        {
            Debug.LogWarning($"Status effect '{effect.name}' is not in the StatusEffectRegistry.");
            return;
        }
 
        double now = Now;
        double end = effect.Duration > 0f ? now + effect.Duration : -1.0;
 
        int i = IndexOf(id);
        if (i < 0)
        {
            _entries.Add(new StatusEntry { EffectId = id, Stacks = 1, EndTime = end, Source = source });
            _nextTick.Add(now + effect.TickInterval);
            return;
        }
 
        StatusEntry entry = _entries[i];
        switch (effect.Stacking)
        {
            case StatusStacking.Ignore:
                return;
            case StatusStacking.Refresh:
                entry.EndTime = end;
                break;
            case StatusStacking.Stack:
                entry.Stacks = Mathf.Min(entry.Stacks + 1, effect.MaxStacks);
                entry.EndTime = end;
                break;
            case StatusStacking.Extend:
                entry.EndTime = entry.EndTime < 0.0 || end < 0.0 ? -1.0 : entry.EndTime + effect.Duration;
                break;
        }
        entry.Source = source;
        _entries[i] = entry;
    }
 
    public void Remove(StatusEffect effect)
    {
        if (!IsServer || !IsSpawned) return;
        int i = IndexOf(IdOf(effect));
        if (i >= 0) RemoveAt(i);
    }
 
    public void ClearAll()
    {
        if (!IsServer || !IsSpawned) return;
        _entries.Clear();
        _nextTick.Clear();
    }
 
    public void RequestApply(StatusEffect effect)
    {
        if (IsServer) Apply(effect);
        else if (IsSpawned && IdOf(effect) >= 0) ApplyServerRpc(IdOf(effect));
    }
 
    public void RequestClearAll()
    {
        if (IsServer) ClearAll();
        else if (IsSpawned) ClearAllServerRpc();
    }
 
    [ServerRpc(RequireOwnership = false)]
    private void ApplyServerRpc(int effectId) => Apply(Registry.Get(effectId));
 
    [ServerRpc(RequireOwnership = false)]
    private void ClearAllServerRpc() => ClearAll();
 
    // ----- update ----------------------------------------------------------------
 
    private void Update()
    {
        if (!IsSpawned) return;
        if (IsServer) ServerUpdate();
        if (Local == this) ApplyToPlayer();
    }
 
    private void ServerUpdate()
    {
        double now = Now;
 
        for (int i = _entries.Count - 1; i >= 0; i--)
        {
            StatusEntry entry = _entries[i];
            StatusEffect effect = Registry != null ? Registry.Get(entry.EffectId) : null;
            if (effect == null)
            {
                RemoveAt(i);
                continue;
            }
 
            if (effect.TickInterval > 0f && now >= _nextTick[i])
            {
                _nextTick[i] = now + effect.TickInterval;
                effect.Tick(this, entry.Stacks, entry.Source);
                if (!IsSpawned) return;
            }
 
            if (entry.EndTime >= 0.0 && now >= entry.EndTime) RemoveAt(i);
        }
    }
 
    private void ApplyToPlayer()
    {
        StatusModifiers mods = Mods;
 
        _player.SpeedMultiplier = mods.SpeedMultiplier;
        _player.MovementReversed = mods.ReverseMovement;
        AudioListener.volume = mods.VolumeMultiplier;
 
        if (mods.CameraSway > 0f)
        {
            float t = Time.time * _swaySpeed * Mathf.PI * 2f;
            _player.CameraSway = new Vector3(Mathf.Sin(t * 0.5f) * 0.5f, 0f, Mathf.Sin(t)) * mods.CameraSway;
        }
        else
        {
            _player.CameraSway = Vector3.zero;
        }
    }
 
    // ----- helpers -----------------------------------------------------------------
 
    private StatusModifiers Collect()
    {
        StatusModifiers mods = StatusModifiers.None;
        if (!IsSpawned || Registry == null) return mods;
 
        double now = Now;
        for (int i = 0; i < _entries.Count; i++)
        {
            StatusEntry entry = _entries[i];
            StatusEffect effect = Registry.Get(entry.EffectId);
            if (effect == null) continue;
 
            float remaining = entry.EndTime < 0.0 ? float.PositiveInfinity : (float)(entry.EndTime - now);
            if (remaining <= 0f) continue;
 
            effect.Modify(ref mods, entry.Stacks, remaining);
        }
        return mods;
    }
 
    private static int IdOf(StatusEffect effect) => Registry != null ? Registry.IdOf(effect) : -1;
 
    private int IndexOf(int effectId)
    {
        if (effectId < 0) return -1;
        for (int i = 0; i < _entries.Count; i++)
            if (_entries[i].EffectId == effectId) return i;
        return -1;
    }
 
    private void RemoveAt(int i)
    {
        _entries.RemoveAt(i);
        _nextTick.RemoveAt(i);
    }
 
    private void OnEntriesChanged(NetworkListEvent<StatusEntry> change) => _modsFrame = -1;
}