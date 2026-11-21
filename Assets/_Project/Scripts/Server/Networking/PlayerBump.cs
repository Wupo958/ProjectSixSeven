using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
 
public sealed class PlayerBump : NetworkBehaviour
{
    [Tooltip("Closing speed (m/s) needed to knock items loose. Walk = 4, sprint = 8, crouch = 2.")]
    [SerializeField] private float _minImpactSpeed = 6f;
 
    [Tooltip("Extra distance on top of the two bodies touching that still counts as a hit (m).")]
    [SerializeField] private float _hitMargin = 0.1f;
 
    [Tooltip("Seconds both players are immune after a bump, so one collision only counts once.")]
    [SerializeField] private float _cooldown = 1f;
 
    [Tooltip("How often the server checks (s). Lower = more precise, slightly more CPU.")]
    [SerializeField] private float _checkInterval = 0.1f;
 
    [Tooltip("Faster than this (m/s) is a teleport (spawn, respawn, seat), not running.")]
    [SerializeField] private float _maxRunSpeed = 20f;
 
    [Tooltip("Log every touch with its speed to the console. Handy for tuning Min Impact Speed.")]
    [SerializeField] private bool _logBumps = true;
 
    private static readonly List<PlayerBump> s_all = new List<PlayerBump>();
 
    private PlayerCarriageAttachment _attachment;
    private PlayerHealth _health;
    private PlayerInventory _inventory;
    private CharacterController _body;
 
    private Vector3 _previous;
    private Vector3 _current;
    private Vector3 _velocity;
    private bool _hasSample;
    private bool _valid;
    private float _immuneUntil;
    private float _lastCheck;
 
    private float Radius => _body.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.z);
 
    private void Awake()
    {
        _attachment = GetComponent<PlayerCarriageAttachment>();
        _health = GetComponent<PlayerHealth>();
        _inventory = GetComponentInChildren<PlayerInventory>();
        _body = GetComponent<CharacterController>();
    }
 
    public override void OnNetworkSpawn()
    {
        if (!s_all.Contains(this)) s_all.Add(this);
        _lastCheck = Time.time;
    }
 
    public override void OnNetworkDespawn() => s_all.Remove(this);
 
    public override void OnDestroy()
    {
        s_all.Remove(this);
        base.OnDestroy();
    }
 
    // ----- server ------------------------------------------------------------
 
    private void Update()
    {
        if (!IsServer || s_all.Count < 2 || s_all[0] != this) return;
 
        float now = Time.time;
        float dt = now - _lastCheck;
        if (dt < _checkInterval) return;
        _lastCheck = now;
 
        Transform reference = TrainHealth.Instance != null ? TrainHealth.Instance.CarryReference : null;
        if (reference == null) return;
 
        for (int i = 0; i < s_all.Count; i++) s_all[i].Sample(reference, dt);
 
        for (int i = 0; i < s_all.Count; i++)
            for (int j = i + 1; j < s_all.Count; j++)
                Check(s_all[i], s_all[j], reference, now);
    }
 
    private void Sample(Transform reference, float dt)
    {
        Vector3 position = _attachment.PublishedLocalPosition;
        bool active = _health == null || !_health.IsDead;
 
        if (!active || !_hasSample)
        {
            _hasSample = active;
            _previous = _current = position;
            _velocity = Vector3.zero;
            _valid = false;
            return;
        }
 
        _previous = _current;
        _current = position;
        Vector3 raw = (_current - _previous) / dt;
 
        if (reference.TransformVector(raw).magnitude > _maxRunSpeed)
        {
            _velocity = Vector3.zero;
            _valid = false;
            return;
        }
 
        _velocity = Vector3.Lerp(_velocity, raw, 0.5f);
        _valid = true;
    }
 
    private void Check(PlayerBump a, PlayerBump b, Transform reference, float now)
    {
        if (!a._valid || !b._valid) return;
        if (now < a._immuneUntil || now < b._immuneUntil) return;
 
        float touch = a.Radius + b.Radius + _hitMargin;
 
        Vector3 before = reference.TransformVector(b._previous - a._previous);
        Vector3 after = reference.TransformVector(b._current - a._current);
 
        if (before.magnitude < touch) return;
 
        Vector3 path = after - before;
        float t = path.sqrMagnitude > 1e-6f ? Mathf.Clamp01(-Vector3.Dot(before, path) / path.sqrMagnitude) : 0f;
        if ((before + path * t).magnitude >= touch) return;
 
        Vector3 relative = reference.TransformVector(b._velocity - a._velocity);
        float closing = -Vector3.Dot(relative, before.normalized);
        bool hit = closing >= _minImpactSpeed;
 
        if (_logBumps)
            Debug.Log($"Player bump {a.OwnerClientId} <-> {b.OwnerClientId}: {closing:0.0} m/s " +
                      (hit ? "-> items dropped" : $"(needs {_minImpactSpeed:0.0})"));
 
        if (!hit) return;
 
        a._immuneUntil = b._immuneUntil = now + _cooldown;
        a.DropItemClientRpc();
        b.DropItemClientRpc();
    }
 
    // ----- owner -------------------------------------------------------------
 
    [ClientRpc]
    private void DropItemClientRpc()
    {
        if (IsOwner && _inventory != null && _inventory.HasItem) _inventory.Drop();
    }
}