using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public sealed class TrainConfig : NetworkBehaviour
{
    public static TrainConfig Instance { get; private set; }
 
    public event Action LayoutChanged;
 
    [SerializeField] private WagonHalfRegistry _registry;
 
    [Tooltip("Layout the session starts with.")]
    [SerializeField] private List<WagonSlotData> _defaultLayout = new List<WagonSlotData>();
 
    [Tooltip("Largest train players may build.")]
    [SerializeField] private int _maxWagons = 8;
 
    private readonly NetworkList<WagonSlotData> _layout = new NetworkList<WagonSlotData>();
 
    public WagonHalfRegistry Registry => _registry;
    public int WagonCount => _layout.Count;
    public int MaxWagons => _maxWagons;
    public WagonSlotData GetWagon(int i) => _layout[i];
 
    public List<WagonSlotData> Snapshot()
    {
        var slots = new List<WagonSlotData>(_layout.Count);
        for (int i = 0; i < _layout.Count; i++) slots.Add(_layout[i]);
        return slots;
    }
 
    public override void OnNetworkSpawn()
    {
        Instance = this;
 
        DontDestroyOnLoad(gameObject);
 
        _layout.OnListChanged += OnListChanged;
 
        if (IsServer && _layout.Count == 0)
        {
            foreach (WagonSlotData slot in _defaultLayout) _layout.Add(slot);
        }
 
        LayoutChanged?.Invoke();
    }
 
    public override void OnNetworkDespawn()
    {
        _layout.OnListChanged -= OnListChanged;
        if (Instance == this) Instance = null;
    }
 
    private void OnListChanged(NetworkListEvent<WagonSlotData> _) => LayoutChanged?.Invoke();
 
    // ----- editing (called by the builder UI) ------------------------------
 
    [ServerRpc(RequireOwnership = false)]
    public void AddWagonServerRpc(int frontId, int backId)
    {
        if (_layout.Count >= _maxWagons) return;
        _layout.Add(new WagonSlotData(frontId, backId));
    }
 
    [ServerRpc(RequireOwnership = false)]
    public void SetWagonServerRpc(int index, int frontId, int backId)
    {
        if (index < 0 || index >= _layout.Count) return;
        _layout[index] = new WagonSlotData(frontId, backId);
    }
 
    [ServerRpc(RequireOwnership = false)]
    public void RemoveWagonServerRpc(int index)
    {
        if (index < 0 || index >= _layout.Count) return;
        _layout.RemoveAt(index);
    }
 
    [ServerRpc(RequireOwnership = false)]
    public void ClearServerRpc() => _layout.Clear();
}
