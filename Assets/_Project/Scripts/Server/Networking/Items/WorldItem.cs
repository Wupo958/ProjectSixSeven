using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
 
[RequireComponent(typeof(NetworkObject))]
public sealed class WorldItem : NetworkBehaviour
{
    private const byte Flying = 0;
    private const byte OnCar = 1;
    private const float MaxPickupDistance = 5f;   //server sanity check, generous for latency
    private const int MaxBounces = 4;
 
    [Header("Flight")]
    [Tooltip("Size of the item for hit detection, in metres.")]
    [SerializeField] private float _hitRadius = 0.1f;
 
    [Tooltip("An item still in the air after this many seconds fell into nothing and is removed.")]
    [SerializeField] private float _maxFlightTime = 6f;
 
    [Tooltip("Surfaces at least this flat count as floor to land on; steeper ones are walls.")]
    [Range(0f, 1f)]
    [SerializeField] private float _floorNormalMinY = 0.6f;
 
    [Header("Landing")]
    [Tooltip("Share of the falling speed kept as an upward bounce. 0 = no bounce.")]
    [Range(0f, 0.9f)]
    [SerializeField] private float _bounciness = 0.35f;
 
    [Tooltip("Bounces slower than this (m/s) stop.")]
    [SerializeField] private float _minBounceSpeed = 0.6f;
 
    [Tooltip("Share of the sideways speed kept when the item hits the floor.")]
    [Range(0f, 1f)]
    [SerializeField] private float _landingGrip = 0.6f;
 
    [Tooltip("How quickly a sliding item stops, in m/s². Higher = stops sooner.")]
    [SerializeField] private float _slideFriction = 6f;
 
    [Tooltip("How much the item turns while sliding, in degrees per metre.")]
    [SerializeField] private float _spinPerMetre = 120f;
 
    [Tooltip("Share of the speed kept when the item bounces off a wall.")]
    [Range(0f, 1f)]
    [SerializeField] private float _wallBounce = 0.5f;
 
    private static readonly List<WorldItem> s_flying = new List<WorldItem>();
    private static readonly RaycastHit[] s_hits = new RaycastHit[16];
 
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => s_flying.Clear();
 
    private readonly NetworkVariable<int> _itemId = new NetworkVariable<int>(ItemRegistry.Empty);
    private readonly NetworkVariable<byte> _state = new NetworkVariable<byte>(Flying);
 
    //flying (world space)
    private readonly NetworkVariable<Vector3> _launchPos = new NetworkVariable<Vector3>();
    private readonly NetworkVariable<Vector3> _launchVel = new NetworkVariable<Vector3>();
    private readonly NetworkVariable<double> _launchTime = new NetworkVariable<double>();
 
    //on a car (car-local space)
    private readonly NetworkVariable<int> _car = new NetworkVariable<int>(-1);
    private readonly NetworkVariable<Vector3> _restPos = new NetworkVariable<Vector3>();
    private readonly NetworkVariable<Quaternion> _restRot = new NetworkVariable<Quaternion>(Quaternion.identity);
    private readonly NetworkVariable<Vector3> _slideVel = new NetworkVariable<Vector3>();
    private readonly NetworkVariable<float> _bounceSpeed = new NetworkVariable<float>();
    private readonly NetworkVariable<double> _settleTime = new NetworkVariable<double>();
 
    //server only
    private int _pendingId = ItemRegistry.Empty;
    private Vector3 _pendingPos;
    private Vector3 _pendingVel;
    private bool _claimed;
    private Vector3 _prevLocal;
    private float _settleEnd;
 
    private float _bottomOffset;   //pivot height above items lowest point
 
    public ItemDefinition Definition =>
        ItemRegistry.Active != null ? ItemRegistry.Active.Get(_itemId.Value) : null;
 
    public bool IsFlying => _state.Value == Flying;
 
    public static Transform TrainFrame =>
        TrainHealth.Instance != null ? TrainHealth.Instance.CarryReference : null;
 
    public static Vector3 TrainVelocity =>
        TrainHealth.Instance != null ? TrainHealth.Instance.Velocity : Vector3.zero;
 
    private void Awake()
    {
        if (TryGetComponent(out Rigidbody rb))
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }
 
        foreach (Collider c in GetComponentsInChildren<Collider>()) c.isTrigger = true;
 
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            _bottomOffset = transform.position.y - bounds.min.y;
        }
    }
 
    // ----- spawning (server) -----------------------------------------------
 
    public static void SpawnFlying(GameObject prefab, int itemId, Vector3 position, Vector3 velocity)
    {
        GameObject go = Instantiate(prefab, position, Quaternion.identity);
        if (!go.TryGetComponent(out WorldItem item) || !go.TryGetComponent(out NetworkObject netObj))
        {
            Debug.LogError($"World Prefab '{prefab.name}' needs a NetworkObject and a WorldItem.", prefab);
            Destroy(go);
            return;
        }
 
        item._pendingId = itemId;
        item._pendingPos = position;
        item._pendingVel = velocity;
        netObj.Spawn(true);
    }
 
    public override void OnNetworkSpawn()
    {
        if (IsServer && _pendingId != ItemRegistry.Empty)
        {
            _itemId.Value = _pendingId;
            Launch(_pendingPos, _pendingVel);
        }
 
        _state.OnValueChanged += OnStateChanged;
        if (IsFlying && !s_flying.Contains(this)) s_flying.Add(this);
        UpdatePosition();
    }
 
    public override void OnNetworkDespawn()
    {
        _state.OnValueChanged -= OnStateChanged;
        s_flying.Remove(this);
    }
 
    private void OnStateChanged(byte oldState, byte newState)
    {
        if (newState == Flying) { if (!s_flying.Contains(this)) s_flying.Add(this); }
        else s_flying.Remove(this);
    }
 
    // ----- moving (all clients) ----------------------------------------------
 
    private void LateUpdate()
    {
        if (IsSpawned) UpdatePosition();
    }
 
    private void UpdatePosition()
    {
        if (IsFlying)
        {
            float t = (float)Math.Max(0.0, NetworkManager.ServerTime.Time - _launchTime.Value);
            Vector3 target = _launchPos.Value + _launchVel.Value * t + 0.5f * t * t * Physics.gravity;
 
            if (IsServer) ServerFly(target, t);
            else transform.position = target;
            return;
        }
 
        if (IsServer) ServerSettle();
        if (!IsFlying) PlaceOnCar();
    }
 
    private Transform CurrentCar()
    {
        Trainbuilder train = Trainbuilder.Instance;
        return train != null ? train.GetCar(_car.Value) : null;
    }
 
    private void PlaceOnCar()
    {
        Transform car = CurrentCar();
        if (car == null) return;
 
        SettleAt(SettleT(), out Vector3 local, out Quaternion rot);
        transform.SetPositionAndRotation(car.TransformPoint(local), car.rotation * rot);
    }
 
    // ----- bounce and slide (car-local, identical on every client) ---------------
 
    private float SettleT() =>
        (float)Math.Max(0.0, NetworkManager.ServerTime.Time - _settleTime.Value);
 
    private void SettleAt(float t, out Vector3 local, out Quaternion rot)
    {
        Vector3 v = _slideVel.Value;
        float speed = v.magnitude;
        float slide = 0f;
        Vector3 dir = Vector3.zero;
 
        if (speed > 0.001f)
        {
            dir = v / speed;
            float tt = Mathf.Min(t, speed / Mathf.Max(0.01f, _slideFriction));
            slide = speed * tt - 0.5f * _slideFriction * tt * tt;
        }
 
        local = _restPos.Value + dir * slide + Vector3.up * BounceHeight(_bounceSpeed.Value, t);
 
        float spinSign = dir.x >= 0f ? 1f : -1f;
        rot = Quaternion.Euler(0f, slide * _spinPerMetre * spinSign, 0f) * _restRot.Value;
    }
 
    private float BounceHeight(float v, float t)
    {
        float g = -Physics.gravity.y;
        for (int i = 0; i < MaxBounces && v >= _minBounceSpeed; i++)
        {
            float duration = 2f * v / g;
            if (t < duration) return v * t - 0.5f * g * t * t;
            t -= duration;
            v *= _bounciness;
        }
        return 0f;
    }
 
    private Vector3 SlideVelocityAt(float t)
    {
        Vector3 v = _slideVel.Value;
        float speed = v.magnitude;
        if (speed < 0.001f) return Vector3.zero;
        return v / speed * Mathf.Max(0f, speed - _slideFriction * t);
    }
 
    private float SettleDuration()
    {
        float slide = _slideVel.Value.magnitude / Mathf.Max(0.01f, _slideFriction);
 
        float bounce = 0f, v = _bounceSpeed.Value, g = -Physics.gravity.y;
        for (int i = 0; i < MaxBounces && v >= _minBounceSpeed; i++)
        {
            bounce += 2f * v / g;
            v *= _bounciness;
        }
        return Mathf.Max(slide, bounce);
    }
 
    private void StartSettle(Vector3 localStart, Vector3 localSlideVel, float bounceSpeed, Quaternion rot)
    {
        _restPos.Value = localStart;
        _slideVel.Value = new Vector3(localSlideVel.x, 0f, localSlideVel.z);
        _bounceSpeed.Value = bounceSpeed >= _minBounceSpeed ? bounceSpeed : 0f;
        _restRot.Value = rot;
        _settleTime.Value = NetworkManager.ServerTime.Time;
 
        _prevLocal = localStart;
        _settleEnd = SettleDuration();
    }
 
    // ----- server checks ---------------------------------------------------------
 
    private void ServerFly(Vector3 target, float flightTime)
    {
        Vector3 from = transform.position;
        Vector3 delta = target - from;
        float distance = delta.magnitude;
 
        if (distance > 0.0001f)
        {
            Vector3 dir = delta / distance;
            Physics.SyncTransforms();  
 
            if (FindHit(from, dir, distance, false, out RaycastHit hit))
            {
                Vector3 velocity = _launchVel.Value + Physics.gravity * flightTime;
                OnImpact(hit, from + dir * hit.distance, velocity);
                return;
            }
        }
 
        transform.position = target;
        if (flightTime > _maxFlightTime) Despawn("fell off the train");
    }
 
    private void ServerSettle()
    {
        float t = SettleT();
        if (t > _settleEnd + 0.05f) return;   
 
        Transform car = CurrentCar();
        if (car == null)
        {
            Despawn("its wagon is gone");
            return;
        }
 
        SettleAt(t, out Vector3 local, out _);
        Vector3 up = car.up;
 
        Vector3 from = car.TransformPoint(_prevLocal) + up * _hitRadius;
        Vector3 to = car.TransformPoint(local) + up * _hitRadius;
        Vector3 delta = to - from;
        float distance = delta.magnitude;
 
        if (distance > 0.0001f)
        {
            Vector3 dir = delta / distance;
            Physics.SyncTransforms();
 
            if (FindHit(from, dir, distance, true, out RaycastHit wall))
            {
                BounceOffWall(car, wall, from + dir * wall.distance, t);
                return;
            }
        }
 
        if (!Physics.Raycast(to, -up, _hitRadius + _bottomOffset + 0.5f, ~0, QueryTriggerInteraction.Ignore))
        {
            Launch(car.TransformPoint(local), car.TransformDirection(SlideVelocityAt(t)) + TrainVelocity);
            return;
        }
 
        _prevLocal = local;
    }
 
    private bool FindHit(Vector3 from, Vector3 dir, float distance, bool wallsOnly, out RaycastHit best)
    {
        best = default;
        bool found = false;
        float bestDistance = float.MaxValue;
 
        int count = Physics.SphereCastNonAlloc(from, _hitRadius, dir, s_hits, distance, ~0,
                                               QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            RaycastHit h = s_hits[i];
            if (h.distance <= 0f) continue;                                              
            if (h.collider.GetComponentInParent<CharacterController>() != null) continue; 
            if (wallsOnly && h.normal.y >= _floorNormalMinY) continue;                  
            if (h.distance < bestDistance)
            {
                bestDistance = h.distance;
                best = h;
                found = true;
            }
        }
        return found;
    }
 
    private void OnImpact(RaycastHit hit, Vector3 center, Vector3 velocity)
    {
        Trainbuilder train = Trainbuilder.Instance;
        int car = train != null ? train.CarIndexOf(hit.collider.transform) : -1;
 
        if (car < 0)
        {
            Despawn("fell off the train");
            return;
        }
 
        ItemDefinition def = Definition;
        if (def != null && def.Breakable)
        {
            Despawn("broke on impact");
            return;
        }
 
        if (hit.normal.y >= _floorNormalMinY)
        {
            Land(train.GetCar(car), car, hit.point, velocity);
            return;
        }
 
        Vector3 relative = velocity - TrainVelocity;
        Launch(center + hit.normal * 0.02f, TrainVelocity + Vector3.Reflect(relative, hit.normal) * _wallBounce);
    }
 
    private void Land(Transform car, int carIndex, Vector3 point, Vector3 worldVelocity)
    {
        Vector3 relative = car.InverseTransformDirection(worldVelocity - TrainVelocity);
        float yaw = (Quaternion.Inverse(car.rotation) * transform.rotation).eulerAngles.y;
 
        _car.Value = carIndex;
        StartSettle(car.InverseTransformPoint(point) + Vector3.up * _bottomOffset,
                    new Vector3(relative.x, 0f, relative.z) * _landingGrip,
                    -relative.y * _bounciness,
                    Quaternion.Euler(0f, yaw, 0f));
        _state.Value = OnCar;
 
        PlaceOnCar();
    }
 
    private void BounceOffWall(Transform car, RaycastHit hit, Vector3 centerAtHit, float t)
    {
        Vector3 normal = car.InverseTransformDirection(hit.normal);
        normal.y = 0f;
 
        Vector3 velocity = SlideVelocityAt(t);
        if (normal.sqrMagnitude > 0.0001f) velocity = Vector3.Reflect(velocity, normal.normalized);
 
        Vector3 local = car.InverseTransformPoint(centerAtHit - car.up * _hitRadius);
        local.y = _restPos.Value.y; 
 
        SettleAt(t, out _, out Quaternion rot);
        StartSettle(local, velocity * _wallBounce, 0f, rot);
    }
 
    private void Launch(Vector3 position, Vector3 velocity)
    {
        _launchPos.Value = position;
        _launchVel.Value = velocity;
        _launchTime.Value = NetworkManager.ServerTime.Time;
        _state.Value = Flying;
        transform.position = position;
    }
 
    private void Despawn(string reason)
    {
        if (!IsServer || !IsSpawned) return;
        _claimed = true;
        Debug.Log($"{name}: {reason}");
        NetworkObject.Despawn(true);
    }
 
    // ----- picking up and catching ---------------------------------------------
 
    public void RequestPickUp()
    {
        if (IsSpawned) PickUpServerRpc();
    }
 
    [ServerRpc(RequireOwnership = false)]
    private void PickUpServerRpc(ServerRpcParams rpcParams = default)
    {
        if (_claimed || !IsSpawned) return; 
 
        ulong sender = rpcParams.Receive.SenderClientId;
        if (!NetworkManager.ConnectedClients.TryGetValue(sender, out NetworkClient client) ||
            client.PlayerObject == null) return;
 
        PlayerInventory inventory = client.PlayerObject.GetComponentInChildren<PlayerInventory>();
        if (inventory == null) return;
 
        if (Vector3.Distance(client.PlayerObject.transform.position, transform.position) > MaxPickupDistance)
            return;
 
        _claimed = true;
        inventory.GrantItemClientRpc(_itemId.Value, new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { sender } }
        });
        NetworkObject.Despawn(true);
    }
 
    public static WorldItem FindCatchable(Vector3 eye, float range)
    {
        WorldItem best = null;
        float bestSq = range * range;
 
        foreach (WorldItem item in s_flying)
        {
            if (item == null || !item.IsSpawned) continue;
            float sq = (item.transform.position - eye).sqrMagnitude;
            if (sq <= bestSq)
            {
                bestSq = sq;
                best = item;
            }
        }
        return best;
    }
}