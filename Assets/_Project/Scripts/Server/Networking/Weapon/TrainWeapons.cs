using System;
using Unity.Netcode;
using UnityEngine;
 
public struct WeaponState : INetworkSerializable, IEquatable<WeaponState>
{
    public ulong Occupant;
    public float Pitch;
    public float Yaw;
    public int Loaded;
    public float AmmoMultiplier;
 
    public static WeaponState Empty => new WeaponState
    {
        Occupant = WeaponBase.NoOccupant,
        AmmoMultiplier = 1f
    };
 
    public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
    {
        s.SerializeValue(ref Occupant);
        s.SerializeValue(ref Pitch);
        s.SerializeValue(ref Yaw);
        s.SerializeValue(ref Loaded);
        s.SerializeValue(ref AmmoMultiplier);
    }
 
    public bool Equals(WeaponState o) =>
        Occupant == o.Occupant && Pitch == o.Pitch && Yaw == o.Yaw &&
        Loaded == o.Loaded && AmmoMultiplier == o.AmmoMultiplier;
}
 
[RequireComponent(typeof(NetworkObject))]
public sealed class TrainWeapons : NetworkBehaviour
{
    public static TrainWeapons Instance { get; private set; }
 
    private readonly NetworkList<WeaponState> _states = new NetworkList<WeaponState>();
    private WeaponBase[] _weapons = new WeaponBase[0];
 
    public bool HasState(int i) => i >= 0 && i < _states.Count;
    public WeaponState Get(int i) => _states[i];
 
    public override void OnNetworkSpawn()
    {
        Instance = this;
        if (IsServer) NetworkManager.OnClientDisconnectCallback += OnClientLeft;
        Rebind();
    }
 
    public override void OnNetworkDespawn()
    {
        if (IsServer && NetworkManager != null) NetworkManager.OnClientDisconnectCallback -= OnClientLeft;
        if (Instance == this) Instance = null;
    }
 
    public void Rebind()
    {
        _weapons = GetComponentsInChildren<WeaponBase>(true);
        for (int i = 0; i < _weapons.Length; i++) _weapons[i].Bind(this, i);
 
        if (IsSpawned && IsServer)
        {
            _states.Clear();
            for (int i = 0; i < _weapons.Length; i++) _states.Add(WeaponState.Empty);
        }
    }
 
    // ----- server RPCs (called through WeaponBase) ---------------------------
 
    [ServerRpc(RequireOwnership = false)]
    public void MountServerRpc(int i, ServerRpcParams p = default)
    {
        if (!HasState(i)) return;
        WeaponState s = _states[i];
        if (s.Occupant != WeaponBase.NoOccupant) return;
        s.Occupant = p.Receive.SenderClientId;
        _states[i] = s;
    }
 
    [ServerRpc(RequireOwnership = false)]
    public void DismountServerRpc(int i, ServerRpcParams p = default)
    {
        if (!HasState(i)) return;
        WeaponState s = _states[i];
        if (s.Occupant != p.Receive.SenderClientId) return;
        s.Occupant = WeaponBase.NoOccupant;
        _states[i] = s;
    }
 
    [ServerRpc(RequireOwnership = false)]
    public void SetAimServerRpc(int i, Vector2 aim, ServerRpcParams p = default)
    {
        if (!HasState(i)) return;
        WeaponState s = _states[i];
        if (s.Occupant != p.Receive.SenderClientId) return;
        s.Pitch = aim.x;
        s.Yaw = aim.y;
        _states[i] = s;
    }
 
    [ServerRpc(RequireOwnership = false)]
    public void LoadAmmoServerRpc(int i, float damageMultiplier)
    {
        if (!HasState(i) || _weapons[i] == null) return;
        WeaponState s = _states[i];
        int cap = _weapons[i].Capacity;
        if (s.Loaded >= cap) return;
 
        s.Loaded = Mathf.Min(cap, s.Loaded + _weapons[i].RoundsPerLoad);
        s.AmmoMultiplier = Mathf.Max(0.01f, damageMultiplier);
        _states[i] = s;
    }
 
    [ServerRpc(RequireOwnership = false)]
    public void FireServerRpc(int i, ServerRpcParams p = default)
    {
        if (!HasState(i) || _weapons[i] == null) return;
        WeaponState s = _states[i];
        if (s.Occupant != p.Receive.SenderClientId || s.Loaded <= 0) return;
 
        s.Loaded -= 1;
        _states[i] = s;
        _weapons[i].SpawnProjectile(s.AmmoMultiplier);
    }
 
    private void OnClientLeft(ulong clientId)
    {
        for (int i = 0; i < _states.Count; i++)
        {
            if (_states[i].Occupant != clientId) continue;
            WeaponState s = _states[i];
            s.Occupant = WeaponBase.NoOccupant;
            _states[i] = s;
        }
    }
}
