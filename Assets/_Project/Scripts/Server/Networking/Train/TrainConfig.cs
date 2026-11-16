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

    [Header("Cost")]
    [SerializeField, Min(0)] private int _addWagonCost = 1;
    [SerializeField, Min(0)] private int _changeHalfCost = 1;
    [SerializeField, Min(0)] private int _upgradeCost = 1;

    private readonly NetworkList<WagonSlotData> _layout = new NetworkList<WagonSlotData>();

    public WagonHalfRegistry Registry => _registry;
    public int WagonCount => _layout.Count;
    public int MaxWagons => _maxWagons;
    public int AddWagonCost => _addWagonCost;
    public int ChangeHalfCost => _changeHalfCost;
    public int UpgradeCost => _upgradeCost;
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
        if (_layout.Count >= _maxWagons)
        {
            return;
        }

        if (!IsValidHalf(frontId) || !IsValidHalf(backId))
        {
            return;
        }

        if (!TrySpend(_addWagonCost))
        {
            return;
        }

        _layout.Add(new WagonSlotData(frontId, backId));
    }

    [ServerRpc(RequireOwnership = false)]
    public void SetWagonServerRpc(int index, int frontId, int backId)
    {
        if (index < 0 || index >= _layout.Count)
        {
            return;
        }

        if (!IsValidHalf(frontId) || !IsValidHalf(backId))
        {
            return;
        }

        WagonSlotData current = _layout[index];
        int changedHalves = CountChangedHalves(current, frontId, backId);
        if (changedHalves == 0)
        {
            return;
        }

        if (!TrySpend(changedHalves * _changeHalfCost))
        {
            return;
        }

        _layout[index] = new WagonSlotData(frontId, backId, current.Upgraded);
    }

    [ServerRpc(RequireOwnership = false)]
    public void UpgradeWagonServerRpc(int index)
    {
        if (index < 0 || index >= _layout.Count)
        {
            return;
        }

        if (GameSession.Instance == null || !GameSession.Instance.RunStarted)
        {
            return;
        }

        WagonSlotData current = _layout[index];
        if (current.Upgraded)
        {
            return;
        }

        if (!TrySpend(_upgradeCost))
        {
            return;
        }

        _layout[index] = new WagonSlotData(current.Front, current.Back, true);
    }

    public int CountChangedHalves(WagonSlotData current, int frontId, int backId)
    {
        int changed = 0;
        if (current.Front != frontId)
        {
            changed++;
        }

        if (current.Back != backId)
        {
            changed++;
        }

        return changed;
    }

    private bool IsValidHalf(int id)
    {
        if (id == WagonHalfRegistry.Empty)
        {
            return true;
        }

        return _registry != null && _registry.Get(id) != null;
    }

    private static bool TrySpend(int cost)
    {
        if (GameSession.Instance == null)
        {
            return true;
        }

        return GameSession.Instance.TrySpend(cost);
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
