using Unity.Netcode;
using UnityEngine;
 
public abstract class WeaponBase : MonoBehaviour
{
    public const ulong NoOccupant = ulong.MaxValue;
 
    [Header("Setup")]
    [SerializeField] protected WeaponDefinition _definition;
 
    [Tooltip("Rotates left/right (yaw). Usually the turret base.")]
    [SerializeField] protected Transform _yawPivot;
 
    [Tooltip("Rotates up/down (pitch). Usually the barrel.")]
    [SerializeField] protected Transform _pitchPivot;
 
    [Tooltip("Tick if the pitch pivot is a SIBLING of the yaw pivot instead of a child.")]
    [SerializeField] private bool _pitchPivotIsSibling = false;
 
    [Tooltip("Empty placed at the barrel's real pivot point. Leave null to rotate " +
             "around the mesh's own origin.")]
    [SerializeField] private Transform _pitchPivotPoint;
 
    [Tooltip("Where projectiles leave the barrel.")]
    [SerializeField] protected Transform _muzzle;
 
    [Tooltip("Where the gunner's camera sits while seated.")]
    [SerializeField] protected Transform _cameraAnchor;
 
    [Tooltip("Projectile prefab. Needs NetworkObject + WeaponProjectile.")]
    [SerializeField] protected GameObject _projectilePrefab;
 
    [Header("Networking")]
    [Tooltip("How many times per second the gunner's aim is sent to the server.")]
    [SerializeField] private float _aimSendRate = 20f;
 
    [SerializeField] private float _remoteAimSmoothing = 12f;
 
    private TrainWeapons _hub;
    private int _index = -1;
    private Vector3 _pitchPivotRestPos;
 
    protected float Pitch;
    protected float Yaw;
    private float _nextAimSend;
    private float _nextShotTime;

    public static WeaponBase LocalManned { get; private set; }
    public ItemDefinition LoadedAmmo => ItemRegistry.Active != null ? ItemRegistry.Active.Get(State.AmmoItemId) : null;
    public virtual string HudReadout => null;
 
    // ----- binding ---------------------------------------------------------
 
    public void Bind(TrainWeapons hub, int index)
    {
        _hub = hub;
        _index = index;
    }
 
    public bool IsSpawned => _hub != null && _hub.IsSpawned && _hub.HasState(_index);
 
    private WeaponState State => IsSpawned ? _hub.Get(_index) : WeaponState.Empty;
 
    // ----- public API (unchanged) ---------------------------------------------
 
    public WeaponDefinition Definition => _definition;
    public Transform CameraAnchor => _cameraAnchor;
    public ulong Occupant => State.Occupant;
    public bool IsOccupied => State.Occupant != NoOccupant;
    public int Loaded => State.Loaded;
    public bool NeedsAmmo => Loaded < Capacity;
 
    public int Capacity => _definition != null ? _definition.MagazineCapacity : 1;
    public int RoundsPerLoad => _definition != null ? Mathf.Max(1, _definition.RoundsPerAmmoItem) : 1;
 
    public PlayerInteractor LocalGunner { get; private set; }
    public bool IsLocalGunner => LocalGunner != null;
 
    protected virtual void Awake()
    {
        if (_pitchPivot != null) _pitchPivotRestPos = _pitchPivot.localPosition;
    }
 
    // ----- seat -----------------------------------------------------------
 
    public void RequestMountServerRpc(ulong clientId)
    {
        if (IsSpawned) _hub.MountServerRpc(_index);
    }
 
    public void RequestDismountServerRpc(ulong clientId)
    {
        if (IsSpawned) _hub.DismountServerRpc(_index);
    }
 
    public void BeginLocalControl(PlayerInteractor player)
    {
        LocalGunner = player;
        WeaponState s = State;
        Pitch = s.Pitch;
        Yaw = s.Yaw;
        LocalManned = this;
    }
 
    public void EndLocalControl()
    {
        LocalGunner = null;
        if (LocalManned == this) LocalManned = null;
    }
 
    // ----- ammo -----------------------------------------------------------
 
    public bool AcceptsAmmo(ItemDefinition item)
    {
        if (item == null || _definition == null) return false;
        if (string.IsNullOrEmpty(_definition.AcceptedAmmoTag)) return false;
        return item.Type == ItemType.Ammo && item.AmmoTag == _definition.AcceptedAmmoTag;
    }
 
    public void LoadAmmo(ItemDefinition item)
    {
        if (!IsSpawned || item == null || ItemRegistry.Active == null) return;
        _hub.LoadAmmoServerRpc(_index, ItemRegistry.Active.IdOf(item));
    }
 
    // ----- firing ---------------------------------------------------------
 
    protected bool CanFireNow()
    {
        if (_definition == null || Loaded <= 0) return false;
        return Time.time >= _nextShotTime;
    }
 
    protected void TryFire()
    {
        if (!IsSpawned || !CanFireNow()) return;
 
        _nextShotTime = _definition.FireMode == FireMode.Automatic
            ? Time.time + 60f / Mathf.Max(1f, _definition.RoundsPerMinute)
            : Time.time + 0.25f;
 
        _hub.FireServerRpc(_index);
 
        Pitch = ClampPitch(Pitch + _definition.Recoil);
    }
 
    public void SpawnProjectile(float ammoMultiplier, ulong shooterClientId)
    {
        if (_definition == null || _projectilePrefab == null || _muzzle == null) return;
 
        Vector3 dir = ApplySpread(_muzzle.forward, _definition.BulletSpread);
 
        GameObject go = Instantiate(_projectilePrefab, _muzzle.position, Quaternion.LookRotation(dir));
        go.GetComponent<NetworkObject>().Spawn(destroyWithScene: true);
        go.GetComponent<WeaponProjectile>().Launch(
            dir,
            _definition.ProjectileSpeed,
            _definition.Range,
            _definition.Damage * ammoMultiplier,
            TrainHealth.Instance.Velocity,
            shooterClientId);
    }
 
    private static Vector3 ApplySpread(Vector3 forward, float degrees)
    {
        if (degrees <= 0f) return forward;
        Quaternion offset = Quaternion.Euler(
            Random.Range(-degrees, degrees), Random.Range(-degrees, degrees), 0f);
        return offset * forward;
    }
 
    // ----- per-frame -------------------------------------------------------
 
    protected virtual void Update()
    {
        if (_definition == null || !IsSpawned) return;
 
        if (IsLocalGunner)
        {
            HandleGunnerInput(Time.deltaTime);
            ApplyAim(Pitch, Yaw);
            PushAimToServer();
        }
        else
        {
            WeaponState s = State;
            float t = 1f - Mathf.Exp(-_remoteAimSmoothing * Time.deltaTime);
            Pitch = Mathf.LerpAngle(Pitch, s.Pitch, t);
            Yaw = Mathf.LerpAngle(Yaw, s.Yaw, t);
            ApplyAim(Pitch, Yaw);
        }
    }
 
    protected abstract void HandleGunnerInput(float dt);
 
    protected void ApplyAim(float pitch, float yaw)
    {
        if (_yawPivot != null) _yawPivot.localRotation = Quaternion.Euler(0f, yaw, 0f);
        if (_pitchPivot == null) return;
 
        Quaternion rot = _pitchPivotIsSibling
            ? Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(-pitch, 0f, 0f)
            : Quaternion.Euler(-pitch, 0f, 0f);
 
        _pitchPivot.localRotation = rot;
 
        if (_pitchPivotPoint != null)
        {
            Vector3 p = _pitchPivot.parent != null
                ? _pitchPivot.parent.InverseTransformPoint(_pitchPivotPoint.position)
                : _pitchPivotPoint.position;
 
            _pitchPivot.localPosition = p + rot * (_pitchPivotRestPos - p);
        }
    }
 
    private void PushAimToServer()
    {
        if (Time.time < _nextAimSend) return;
        _nextAimSend = Time.time + 1f / Mathf.Max(1f, _aimSendRate);
        _hub.SetAimServerRpc(_index, new Vector2(Pitch, Yaw));
    }
 
    protected float ClampPitch(float value) =>
        Mathf.Clamp(value, _definition.PitchMin, _definition.PitchMax);
 
    protected float ClampYaw(float value) =>
        _definition.LimitYaw ? Mathf.Clamp(value, _definition.YawMin, _definition.YawMax) : value;
}
