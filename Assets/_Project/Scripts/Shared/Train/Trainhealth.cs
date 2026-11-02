using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(NetworkObject))]
public sealed class TrainHealth : NetworkBehaviour
{
    public static TrainHealth Instance { get; private set; }

    [Header("Damage visuals")]
    [SerializeField] private Material _workingMat;
    [SerializeField] private Material _brokenMat;

    [Header("Locomotive")]
    [Tooltip("Colliders under this transform route damage to the Lok. It has HP but no defects")]
    [SerializeField] private Transform _lok;
    [SerializeField] private int _lokMaxHp = 100;

    [Header("Game over")]
    [Tooltip("Train is destroyed when total current HP drops below this fraction of max")]
    [Range(0f, 1f)]
    [SerializeField] private float _explodeBelowFraction = 0.25f;

    [SerializeField] private string loseScene = "GameLostScreen";

    [Tooltip("What enemies aim at. Defaults to the Lok, else the train root")]
    public Transform AimTarget;

    [Tooltip("A generated car the players ride relative to. Assign the Lok or Wagon 1")]
    public Transform CarryReference;

    [Header("Repair")]
    [SerializeField] private MinigameType _repairMinigame = MinigameType.Mash;
    [Tooltip("Button presses needed to repair one defect.")]
    [SerializeField] private int _repairDifficulty = 10;

    [ContextMenu("Debug: Break A Defect")]
    private void DebugBreak()
    {
        if (!IsServer || _wagons.Length == 0) return;
        for (int p = 0; p < _wagons[0].DamagePoints.Count; p++)
        {
            if (!IsBit(_masks[0], p))
            {
                _masks[0] = _masks[0] | (1 << p);
                Debug.Log($"Broke wagon 0 point {p}");
                return;
            }
        }
    }

    public MinigameType RepairMinigame => _repairMinigame;
    public int RepairDifficulty => _repairDifficulty;

    public event Action OnTrainDestroyed;
    public event Action<int> OnWagonChanged; //wagon index whose broken state changed

    //one broken-points bitmask per wagon
    private readonly NetworkList<int> _masks = new NetworkList<int>();

    private Wagon[] _wagons;
    private int[] _currentHp;
    private int[] _nextBreak;
    private int[] _frontCount;
    private int _lokHp;
    private float _maxTotalHp;
    private bool _dead;



    [Header("Player spawns")]
    [SerializeField] private Transform[] _spawnAnchors;

    public Transform GetSpawnAnchor(ulong clientId)
    {
        if (_spawnAnchors == null || _spawnAnchors.Length == 0) return transform;
        int i = (int)(clientId % (ulong)_spawnAnchors.Length);
        return _spawnAnchors[i] != null ? _spawnAnchors[i] : transform;
    }

    public Vector3 Velocity { get; private set; }
    private Vector3 _lastCarryPos;
    private bool _hasLastCarryPos;

    private void LateUpdate()
    {
        if (CarryReference == null || Time.deltaTime <= 0f) return;

        Vector3 pos = CarryReference.position;
        if (_hasLastCarryPos) Velocity = (pos - _lastCarryPos) / Time.deltaTime;
        _lastCarryPos = pos;
        _hasLastCarryPos = true;
    }

    private void Awake()
    {
        _wagons = GetComponentsInChildren<Wagon>(true);
        int n = _wagons.Length;

        _currentHp = new int[n];
        _nextBreak = new int[n];
        _frontCount = new int[n];
        _maxTotalHp = _lokMaxHp;

        for (int i = 0; i < n; i++)
        {
            Wagon w = _wagons[i];
            int y = w.Defects > 0 ? w.MaxHP / w.Defects : 0;
            _currentHp[i] = w.MaxHP;
            _nextBreak[i] = w.MaxHP - y;
            _frontCount[i] = (w.Front != null && w.Front.DamagePoints != null) ? w.Front.DamagePoints.Length : 0;
            _maxTotalHp += w.MaxHP;
        }

        _lokHp = _lokMaxHp;
        if (AimTarget == null) AimTarget = _lok != null ? _lok : transform;

        if (CarryReference == null) CarryReference = _lok != null ? _lok : transform;

        for (int i = 0; i < _wagons.Length; i++)
        {
            var pts = _wagons[i].DamagePoints;
            for (int p = 0; p < pts.Count; p++)
            {
                if (pts[p] == null) continue;
                RepairPoint rp = pts[p].GetComponent<RepairPoint>() ?? pts[p].AddComponent<RepairPoint>();
                rp.Setup(this, i, p);
            }
        }
    }

    public override void OnNetworkSpawn()
    {
        Instance = this;
        _masks.OnListChanged += OnMaskChanged;

        if (IsServer)
        {
            for (int i = 0; i < _wagons.Length; i++) _masks.Add(0);
        }

        //refresh visuals from whatever state exists
        int count = Mathf.Min(_wagons.Length, _masks.Count);
        for (int i = 0; i < count; i++) ApplyMask(i, _masks[i]);
    }

    public override void OnNetworkDespawn()
    {
        _masks.OnListChanged -= OnMaskChanged;
        if (Instance == this) Instance = null;
    }

    //DMG ENTRY POINTS

    //called by projectiles/enemies
    public void ReportHit(Collider hitCollider, Vector3 hitPos, int damage)
    {
        if (!IsServer || _dead) return;

        Wagon wagon = hitCollider != null ? hitCollider.GetComponentInParent<Wagon>() : null;
        if (wagon != null)
        {
            int index = Array.IndexOf(_wagons, wagon);
            if (index >= 0) DamageWagon(index, damage, hitPos);
        }
        else
        {
            DamageLok(damage);
        }

        CheckTotal();
    }

    private void DamageWagon(int i, int amount, Vector3 hitPos)
    {
        Wagon w = _wagons[i];
        if (w.Defects == 0) return;

        int y = w.MaxHP / w.Defects;
        _currentHp[i] = Mathf.Max(0, _currentHp[i] - amount);

        while (_currentHp[i] <= _nextBreak[i] && CountBroken(i) < w.Defects)
        {
            int explosive = TriggerDefect(i, hitPos);
            _nextBreak[i] -= y;
            if (explosive > 0) _currentHp[i] = Mathf.Max(0, _currentHp[i] - explosive);
        }
    }

    private void DamageLok(int amount)
    {
        _lokHp = Mathf.Max(0, _lokHp - amount);
        if (_lokHp <= 0) TriggerGameOver(); //GAME OVER
    }

    //returns explosive damage if this defect finished off a half.
    private int TriggerDefect(int i, Vector3 hitPos)
    {
        Wagon w = _wagons[i];
        int front = _frontCount[i];
        int total = w.DamagePoints.Count;

        bool hitFront = NearestHalfIsFront(w, front, total, hitPos);

        int point = ClosestUnbroken(i, hitFront ? 0 : front, hitFront ? front : total, hitPos);
        if (point < 0) point = ClosestUnbroken(i, hitFront ? front : 0, hitFront ? total : front, hitPos);
        if (point < 0) return 0;

        _masks[i] = _masks[i] | (1 << point);

        bool pointIsFront = point < front;
        if (HalfFullyBroken(i, pointIsFront ? 0 : front, pointIsFront ? front : total))
        {
            Wagonhalf half = pointIsFront ? w.Front : w.Back;
            return half != null ? half.ExplosiveDamage : 0;
        }
        return 0;
    }

    //REPAIR

    [ServerRpc(RequireOwnership = false)]
    public void RepairDefectServerRpc(int wagonIndex, int pointIndex) => RepairDefect(wagonIndex, pointIndex);

    public void RepairDefect(int wagonIndex, int pointIndex)
    {
        if (!IsServer || _dead || !InRange(wagonIndex)) return;

        int mask = _masks[wagonIndex];
        if (!IsBit(mask, pointIndex)) return;

        _masks[wagonIndex] = mask & ~(1 << pointIndex);

        Wagon w = _wagons[wagonIndex];
        int y = w.Defects > 0 ? w.MaxHP / w.Defects : 0;
        _currentHp[wagonIndex] = Mathf.Min(w.MaxHP, _currentHp[wagonIndex] + Mathf.Max(0, y - 1));
        _nextBreak[wagonIndex] = Mathf.Min(w.MaxHP - y, _nextBreak[wagonIndex] + y);
    }

    //GAME OVER

    private void CheckTotal()
    {
        if (_dead) return;
        float total = _lokHp;
        for (int i = 0; i < _currentHp.Length; i++) total += _currentHp[i];
        if (total < _maxTotalHp * _explodeBelowFraction) TriggerGameOver();
    }

    private void TriggerGameOver()
    {
        if (_dead) return;
        _dead = true;
        Debug.Log("TRAIN DESTROYED - GAME OVER");
        OnTrainDestroyed?.Invoke();
        GameOverClientRpc();
        LoadLoseScene();
    }

    private void LoadLoseScene()
    {
        if (!IsServer)
        {
            return;
        }

        if (PlayerLifecycle.Main != null)
        {
            PlayerLifecycle.Main.DespawnAllPlayers();
        }

        NetworkManager.Singleton.SceneManager.LoadScene(loseScene, LoadSceneMode.Single);
    }

    [ClientRpc]
    private void GameOverClientRpc()
    {
        if (IsServer) return; //already fired locally
        _dead = true;
        Debug.Log("TRAIN DESTROYED - GAME OVER");
        OnTrainDestroyed?.Invoke();
    }

    //STATUS QUERIES

    public bool IsDead => _dead;
    public int WagonCount => _wagons != null ? _wagons.Length : 0;
    public int DefectsOf(int wagon) => InRange(wagon) ? _wagons[wagon].Defects : 0;
    public bool IsDefectBroken(int wagon, int point) => InRange(wagon) && IsBit(_masks[wagon], point);

    //VISUALS + HELPERS

    private void OnMaskChanged(NetworkListEvent<int> e)
    {
        if (e.Index < 0 || e.Index >= _wagons.Length) return;
        ApplyMask(e.Index, _masks[e.Index]);
        OnWagonChanged?.Invoke(e.Index);
    }

    private void ApplyMask(int wagon, int mask)
    {
        IReadOnlyList<GameObject> pts = _wagons[wagon].DamagePoints;
        for (int p = 0; p < pts.Count; p++)
        {
            if (pts[p] == null) continue;
            MeshRenderer mr = pts[p].GetComponent<MeshRenderer>();
            if (mr != null) mr.sharedMaterial = IsBit(mask, p) ? _brokenMat : _workingMat;
        }
    }

    private int CountBroken(int i)
    {
        int m = _masks[i], c = 0;
        while (m != 0) { c += m & 1; m >>= 1; }
        return c;
    }

    private int ClosestUnbroken(int i, int start, int end, Vector3 hitPos)
    {
        IReadOnlyList<GameObject> pts = _wagons[i].DamagePoints;
        int best = -1; float bestD = float.MaxValue;
        for (int p = start; p < end && p < pts.Count; p++)
        {
            if (IsBit(_masks[i], p) || pts[p] == null) continue;
            float d = (pts[p].transform.position - hitPos).sqrMagnitude;
            if (d < bestD) { bestD = d; best = p; }
        }
        return best;
    }

    private bool NearestHalfIsFront(Wagon w, int front, int total, Vector3 hitPos)
        => MinDist(w, 0, front, hitPos) <= MinDist(w, front, total, hitPos);

    private static float MinDist(Wagon w, int start, int end, Vector3 hitPos)
    {
        IReadOnlyList<GameObject> pts = w.DamagePoints; float best = float.MaxValue;
        for (int p = start; p < end && p < pts.Count; p++)
            if (pts[p] != null) best = Mathf.Min(best, (pts[p].transform.position - hitPos).sqrMagnitude);
        return best;
    }

    private bool HalfFullyBroken(int i, int start, int end)
    {
        IReadOnlyList<GameObject> pts = _wagons[i].DamagePoints;
        for (int p = start; p < end && p < pts.Count; p++)
            if (!IsBit(_masks[i], p)) return false;
        return end > start;
    }

    private bool InRange(int wagon) => _wagons != null && wagon >= 0 && wagon < _wagons.Length;
    private static bool IsBit(int mask, int index) => (mask & (1 << index)) != 0;
}

