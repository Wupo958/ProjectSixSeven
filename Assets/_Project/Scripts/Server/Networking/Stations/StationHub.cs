using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
 
[RequireComponent(typeof(NetworkObject))]
public sealed class StationHub : NetworkBehaviour
{
    public const ulong Free = ulong.MaxValue;
 
    public static StationHub Instance { get; private set; }
 
    private readonly NetworkList<ulong> _holders = new NetworkList<ulong>();
    private readonly NetworkList<double> _activeSince = new NetworkList<double>();
    private readonly Dictionary<int, Action<bool>> _pending = new Dictionary<int, Action<bool>>();
    private MinigameStation[] _stations = Array.Empty<MinigameStation>();
 
    public override void OnNetworkSpawn()
    {
        Instance = this;
        if (IsServer) NetworkManager.OnClientDisconnectCallback += OnClientLeft;
        _activeSince.OnListChanged += OnActiveChanged;
        Rebind();
    }
 
    public override void OnNetworkDespawn()
    {
        if (IsServer && NetworkManager != null) NetworkManager.OnClientDisconnectCallback -= OnClientLeft;
        _activeSince.OnListChanged -= OnActiveChanged;
        if (Instance == this) Instance = null;
    }
 
    public void Rebind()
    {
        _stations = GetComponentsInChildren<MinigameStation>(true);
        for (int i = 0; i < _stations.Length; i++) _stations[i].BindStation(this, i);
 
        _pending.Clear();
        if (IsSpawned && IsServer)
        {
            _holders.Clear();
            _activeSince.Clear();
            for (int i = 0; i < _stations.Length; i++)
            {
                _holders.Add(Free);
                _activeSince.Add(-1.0);
            }
        }
 
        ApplyAllStates();
    }
 
    public ulong HolderOf(int i) => i >= 0 && i < _holders.Count ? _holders[i] : Free;
 
    public double ActiveSinceOf(int i) => i >= 0 && i < _activeSince.Count ? _activeSince[i] : -1.0;
 
    // ----- station state (maintenance tasks etc.) ----------------------------------
 
    public void SetActive(int i, bool active)
    {
        if (!IsServer || i < 0 || i >= _activeSince.Count) return;
        bool isActive = _activeSince[i] >= 0.0;
        if (active == isActive) return;
        _activeSince[i] = active ? NetworkManager.ServerTime.Time : -1.0;
    }
 
    [ServerRpc(RequireOwnership = false)]
    public void DeactivateServerRpc(int i) => SetActive(i, false);
 
    [ServerRpc(RequireOwnership = false)]
    public void DebugSetActiveServerRpc(int i, bool active) => SetActive(i, active);
 
    private void OnActiveChanged(NetworkListEvent<double> change)
    {
        if (change.Type == NetworkListEvent<double>.EventType.Value &&
            change.Index >= 0 && change.Index < _stations.Length && _stations[change.Index] != null)
        {
            _stations[change.Index].ApplyStationState(change.Value);
            return;
        }
        ApplyAllStates();
    }
 
    private void ApplyAllStates()
    {
        for (int i = 0; i < _stations.Length; i++)
            if (_stations[i] != null) _stations[i].ApplyStationState(ActiveSinceOf(i));
    }
 
    // ----- locking -------------------------------------------------------------
 
    public void RequestLock(int i, Action<bool> onResult)
    {
        _pending[i] = onResult;
        LockServerRpc(i);
    }
 
    public void Release(int i)
    {
        if (IsSpawned) ReleaseServerRpc(i);
    }
 
    [ServerRpc(RequireOwnership = false)]
    private void LockServerRpc(int i, ServerRpcParams p = default)
    {
        ulong sender = p.Receive.SenderClientId;
        bool valid = i >= 0 && i < _holders.Count;
        bool granted = valid && (_holders[i] == Free || _holders[i] == sender);
        if (granted && _holders[i] != sender) _holders[i] = sender;
 
        LockResultClientRpc(i, granted, new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { sender } }
        });
    }
 
    [ClientRpc]
    private void LockResultClientRpc(int i, bool granted, ClientRpcParams p = default)
    {
        if (_pending.TryGetValue(i, out Action<bool> callback))
        {
            _pending.Remove(i);
            callback(granted);
        }
    }
 
    [ServerRpc(RequireOwnership = false)]
    private void ReleaseServerRpc(int i, ServerRpcParams p = default)
    {
        if (i >= 0 && i < _holders.Count && _holders[i] == p.Receive.SenderClientId)
            _holders[i] = Free;
    }
 
    private void OnClientLeft(ulong clientId)
    {
        for (int i = 0; i < _holders.Count; i++)
            if (_holders[i] == clientId) _holders[i] = Free;
    }
 
    // ----- failures ------------------------------------------------------------
 
    [ServerRpc(RequireOwnership = false)]
    public void FailedServerRpc(int i)
    {
        if (i >= 0 && i < _stations.Length && _stations[i] != null) _stations[i].OnFailedServer();
    }
}