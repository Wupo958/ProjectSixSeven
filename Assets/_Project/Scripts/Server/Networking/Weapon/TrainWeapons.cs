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
    public int AmmoItemId;
    public float WearBase;  
    public double WearTime; 
    public int Faults; 
 
    public static WeaponState Empty => new WeaponState
    {
        Occupant = WeaponBase.NoOccupant,
        AmmoMultiplier = 1f,
        AmmoItemId = ItemRegistry.Empty,
    };
 
    public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
    {
        s.SerializeValue(ref Occupant);
        s.SerializeValue(ref Pitch);
        s.SerializeValue(ref Yaw);
        s.SerializeValue(ref Loaded);
        s.SerializeValue(ref AmmoMultiplier);
        s.SerializeValue(ref AmmoItemId);
        s.SerializeValue(ref WearBase);
        s.SerializeValue(ref WearTime);
        s.SerializeValue(ref Faults);
    }
 
    public bool Equals(WeaponState o) =>
        Occupant == o.Occupant && Pitch == o.Pitch && Yaw == o.Yaw &&
        Loaded == o.Loaded && AmmoMultiplier == o.AmmoMultiplier &&
        AmmoItemId == o.AmmoItemId
        && WearBase == o.WearBase && WearTime == o.WearTime && Faults == o.Faults;
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
            for (int i = 0; i < _weapons.Length; i++) {
                WeaponState fresh = WeaponState.Empty;
                fresh.WearTime = NetworkManager.ServerTime.Time;
                _states.Add(fresh);
            }
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
    public void LoadAmmoServerRpc(int i, int ammoItemId)
    {
        if (!HasState(i) || _weapons[i] == null) return;

        ItemDefinition ammo = ItemRegistry.Active != null ? ItemRegistry.Active.Get(ammoItemId) : null;
        if (ammo == null || !_weapons[i].AcceptsAmmo(ammo)) return;

        WeaponState s = _states[i];
        int cap = _weapons[i].Capacity;
        if (s.Loaded >= cap) return;

        s.Loaded = Mathf.Min(cap, s.Loaded + _weapons[i].RoundsPerLoad);
        s.AmmoMultiplier = Mathf.Max(0.01f, ammo.AmmoDamageMultiplier);
        s.AmmoItemId = ammoItemId;
        _states[i] = s;
    }
 
    [ServerRpc(RequireOwnership = false)]
    public void FireServerRpc(int i, ServerRpcParams p = default)
    {
        if (!HasState(i) || _weapons[i] == null) return;
        WeaponState s = _states[i];
        if (s.Occupant != p.Receive.SenderClientId || s.Loaded <= 0) return;
        if (_weapons[i].IsJammed || (s.Faults & (int)WeaponFault.TriggerJammed) != 0) return;
 
        s.Loaded -= 1;
        SetWear(ref s, WearOf(i) + _weapons[i].Definition.WearPerShot);
        _states[i] = s;
        _weapons[i].SpawnProjectile(s.AmmoMultiplier, s.Occupant);
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

    // ----- wear and faults ---------------------------------------------------------

    public float WearOf(int i)
    {
        if (!HasState(i) || _weapons[i] == null || _weapons[i].Definition == null) return 0f;
        WeaponState s = _states[i];
        float perSecond = _weapons[i].Definition.WearPerMinute / 60f;
        return Mathf.Clamp01(s.WearBase + perSecond * (float)(NetworkManager.ServerTime.Time - s.WearTime));
    }

    private void SetWear(ref WeaponState s, float wear)
    {
        s.WearBase = Mathf.Clamp01(wear);
        s.WearTime = NetworkManager.ServerTime.Time;
    }

    [ServerRpc(RequireOwnership = false)]
    public void MaintainServerRpc(int i)
    {
        if (!HasState(i)) return;
        WeaponState s = _states[i];
        SetWear(ref s, 0f);
        _states[i] = s;
    }

    public void SetFaults(int i, int faults)
    {
        if (!IsServer || !HasState(i)) return;
        WeaponState s = _states[i];
        if (s.Faults == faults) return;
        s.Faults = faults;
        _states[i] = s;
    }

    [ServerRpc(RequireOwnership = false)]
    public void DebugAddWearServerRpc(int i, float amount)
    {
        if (!HasState(i)) return;
        WeaponState s = _states[i];
        SetWear(ref s, WearOf(i) + amount);
        _states[i] = s;
    }

    [ServerRpc(RequireOwnership = false)]
    public void DebugSetFaultsServerRpc(int i, int faults) => SetFaults(i, faults);
}
