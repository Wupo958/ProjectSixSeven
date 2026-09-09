using System;
using Unity.Netcode;
using UnityEngine;

public class Carriage : NetworkBehaviour
{
    public static Carriage Main { get; private set; }

    [SerializeField] private Transform[] _spawnAnchors;
    [SerializeField] private GameObject[] _damagePoints;
    [SerializeField] private int _trainHpPerPoint;

    [SerializeField] private Material _workingMat;
    [SerializeField] private Material _brokenMat;

    private int _maxHp;
    private int _currentHp;
    private int _nextBreakHp;

    // one bit per damage point, the server owns it so every client shows the same damage
    private readonly NetworkVariable<int> _brokenPoints =
        new NetworkVariable<int>(0,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private void Awake()
    {
        _maxHp = _damagePoints.Length * _trainHpPerPoint;
        _currentHp = _maxHp;
        Debug.Log(_maxHp);
        _nextBreakHp = _maxHp - (_maxHp / _damagePoints.Length);
        Debug.Log(_nextBreakHp);
    }

    public override void OnNetworkSpawn()
    {
        _brokenPoints.OnValueChanged += OnBrokenPointsChanged;
        // late joiners have to catch up on the damage that happened before they connected
        ApplyBrokenPoints(_brokenPoints.Value);
    }

    public override void OnNetworkDespawn()
    {
        _brokenPoints.OnValueChanged -= OnBrokenPointsChanged;
    }

    private void OnEnable()
    {
        if (Main == null)
        {
            Main = this;
        }
    }

    private void OnDisable()
    {
        if (Main == this)
        {
            Main = null;
        }
    }

    public Transform GetSpawnAnchor(ulong clientId)
    {
        if (_spawnAnchors == null || _spawnAnchors.Length == 0)
        {
            return transform;
        }

        int index = (int)(clientId % (ulong)_spawnAnchors.Length);
        return _spawnAnchors[index] != null ? _spawnAnchors[index] : transform;
    }

    public void TakeDamage(int damageAmount, Vector3 hitPos)
    {
        // only the server keeps score, clients just get told what broke
        if (!IsServer)
        {
            return;
        }

        _currentHp -= damageAmount;
        if (_currentHp < _maxHp / 4)
        {
            //kill train
            Debug.Log("Train Dead");
        }
        else if (_currentHp < _nextBreakHp)
        {
            DamageClosestPoint(hitPos);
            _nextBreakHp = _nextBreakHp - (_maxHp / _damagePoints.Length);
            Debug.Log("Broke Object");
        }
    }


    public void DamageClosestPoint(Vector3 hitPos)
    {
        int closest = -1;
        float closestDistance = float.MaxValue;

        for (int i = 0; i < _damagePoints.Length; i++)
        {
            if (IsBroken(_brokenPoints.Value, i))
            {
                continue;
            }

            float distance = Vector3.Distance(_damagePoints[i].transform.position, hitPos);
            if (distance < closestDistance)
            {
                closest = i;
                closestDistance = distance;
            }
        }

        // everything is broken already
        if (closest < 0)
        {
            return;
        }

        _brokenPoints.Value = _brokenPoints.Value | (1 << closest);
    }

    private void OnBrokenPointsChanged(int oldValue, int newValue)
    {
        ApplyBrokenPoints(newValue);
    }

    private void ApplyBrokenPoints(int mask)
    {
        for (int i = 0; i < _damagePoints.Length; i++)
        {
            _damagePoints[i].GetComponent<MeshRenderer>().sharedMaterial =
                IsBroken(mask, i) ? _brokenMat : _workingMat;
        }
    }

    private static bool IsBroken(int mask, int index)
    {
        return (mask & (1 << index)) != 0;
    }
}
