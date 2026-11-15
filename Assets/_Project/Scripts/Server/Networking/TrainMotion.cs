using Unity.Netcode;
using UnityEngine;
 
public sealed class TrainMotion : NetworkBehaviour
{
    public static TrainMotion Instance { get; private set; }
 
    [Tooltip("Top speed in m/s. 30 is roughly 110 km/h.")]
    [SerializeField] private float _maxSpeed = 30f;
 
    [Tooltip("Speed the train drives at on its own until another system sets a target.")]
    [SerializeField] private float _startSpeed = 30f;
 
    [Tooltip("Seconds after the level starts before the train sets off.")]
    [SerializeField] private float _startDelay = 5f;
 
    [Tooltip("Speeding up, in m/s gained per second.")]
    [SerializeField] private float _acceleration = 2f;
 
    [Tooltip("Slowing down, in m/s lost per second.")]
    [SerializeField] private float _braking = 4f;
 
    [Header("Read only")]
    [SerializeField] private float _currentSpeed;
    [SerializeField] private float _currentTarget;
    [SerializeField] private float _currentDistance;
 
    private struct Segment : INetworkSerializeByMemcpy
    {
        public double StartTime;
        public float StartDistance;
        public float StartSpeed;
        public float TargetSpeed;
        public float Rate;
    }
 
    private readonly NetworkVariable<Segment> _synced = new NetworkVariable<Segment>();
 
    private Segment _segment;
    private float _target;
    private float _limit = float.PositiveInfinity;
 
    private Trainbuilder _builder;
    private int _cachedFrame = -1;
    private float _speed;
    private float _distance;
 
    public float Speed { get { Refresh(); return _speed; } }
    public float Distance { get { Refresh(); return _distance; } }
    public float TargetSpeed => _segment.TargetSpeed;
    public float MaxSpeed => _maxSpeed;
 
    private static bool Networked => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
    private bool CanWrite => !Networked || IsServer;
 
    private void Awake()
    {
        Instance = this;
        _builder = GetComponent<Trainbuilder>();
    }
 
    private void Start()
    {
        if (!Networked) StartLevel();
    }
 
    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            StartLevel();
        }
        else
        {
            _segment = _synced.Value;
            _synced.OnValueChanged += OnSegmentChanged;
        }
    }
 
    public override void OnNetworkDespawn()
    {
        _synced.OnValueChanged -= OnSegmentChanged;
    }
 
    public override void OnDestroy()
    {
        if (Instance == this) Instance = null;
        base.OnDestroy();
    }
 
    private void OnSegmentChanged(Segment previous, Segment current)
    {
        _segment = current;
        _cachedFrame = -1;
    }
 
    private void StartLevel()
    {
        _target = Mathf.Clamp(_startSpeed, 0f, _maxSpeed);
        StartSegment(Now() + _startDelay, 0f, 0f, Mathf.Min(_target, _limit));
    }
 
    // ----- interface for other systems --------------------------------------
 
    public void SetTargetSpeed(float speed)
    {
        if (!CanWrite) return;
        _target = Mathf.Clamp(speed, 0f, _maxSpeed);
        ApplyTarget();
    }
 
    public void SetSpeedLimit(float limit)
    {
        if (!CanWrite) return;
        _limit = Mathf.Max(0f, limit);
        ApplyTarget();
    }
 
    public void RequestTargetSpeed(float speed)
    {
        if (CanWrite) SetTargetSpeed(speed);
        else if (IsSpawned) RequestTargetSpeedServerRpc(speed);
    }
 
    [ServerRpc(RequireOwnership = false)]
    private void RequestTargetSpeedServerRpc(float speed) => SetTargetSpeed(speed);
 
    // ----- motion ------------------------------------------------------------
 
    private void ApplyTarget()
    {
        float effective = Mathf.Min(_target, _limit);
        if (Mathf.Abs(effective - _segment.TargetSpeed) < 0.01f) return;
 
        double from = System.Math.Max(Now(), _segment.StartTime);
        Evaluate(from, out float speed, out float distance);
        StartSegment(from, distance, speed, effective);
    }
 
    private void StartSegment(double time, float distance, float speed, float target)
    {
        _segment = new Segment
        {
            StartTime = time,
            StartDistance = distance,
            StartSpeed = speed,
            TargetSpeed = target,
            Rate = target > speed ? _acceleration : _braking
        };
        _cachedFrame = -1;
 
        if (Networked && IsServer) _synced.Value = _segment;
    }
 
    private void Evaluate(double now, out float speed, out float distance)
    {
        Segment s = _segment;
        float t = (float)(now - s.StartTime);
 
        if (t <= 0f)
        {
            speed = s.StartSpeed;
            distance = s.StartDistance;
        }
        else
        {
            float diff = s.TargetSpeed - s.StartSpeed;
            float ramp = s.Rate > 0f ? Mathf.Abs(diff) / s.Rate : 0f;
 
            if (t < ramp)
            {
                speed = s.StartSpeed + Mathf.Sign(diff) * s.Rate * t;
                distance = s.StartDistance + (s.StartSpeed + speed) * 0.5f * t;
            }
            else
            {
                speed = s.TargetSpeed;
                distance = s.StartDistance + (s.StartSpeed + s.TargetSpeed) * 0.5f * ramp
                         + s.TargetSpeed * (t - ramp);
            }
        }

        float max = _builder != null ? _builder.MaxTravel : float.PositiveInfinity;
        if (distance >= max)
        {
            distance = max;
            speed = 0f;
        }
    }
 
    private void Refresh()
    {
        if (_cachedFrame == Time.frameCount) return;
        _cachedFrame = Time.frameCount;
 
        Evaluate(Now(), out _speed, out _distance);
        _currentSpeed = _speed;
        _currentTarget = _segment.TargetSpeed;
        _currentDistance = _distance;
    }
 
    private double Now() => Networked ? NetworkManager.Singleton.ServerTime.Time : Time.timeSinceLevelLoad;
 
    // ----- debug (Inspector: menu on this component, Play mode only) ---------
 
    [ContextMenu("Debug: Stop")]
    private void DebugStop() => RequestTargetSpeed(0f);
 
    [ContextMenu("Debug: Half Speed")]
    private void DebugHalf() => RequestTargetSpeed(_maxSpeed * 0.5f);
 
    [ContextMenu("Debug: Full Speed")]
    private void DebugFull() => RequestTargetSpeed(_maxSpeed);
}