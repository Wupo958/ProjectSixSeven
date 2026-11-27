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
    private readonly Dictionary<int, Action<bool>> _pending = new Dictionary<int, Action<bool>>();
    private MinigameStation[] _stations = Array.Empty<MinigameStation>();
 
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
        _stations = GetComponentsInChildren<MinigameStation>(true);
        for (int i = 0; i < _stations.Length; i++) _stations[i].BindStation(this, i);
 
        _pending.Clear();
        if (IsSpawned && IsServer)
        {
            _holders.Clear();
            for (int i = 0; i < _stations.Length; i++) _holders.Add(Free);
        }
    }
 
    public ulong HolderOf(int i) => i >= 0 && i < _holders.Count ? _holders[i] : Free;
 
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