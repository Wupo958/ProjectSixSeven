using Unity.Netcode;
using UnityEngine;

public abstract class WeaponBase : NetworkBehaviour
{
    public const ulong NoOccupant = ulong.MaxValue;
 
    [Header("Setup")]
    [SerializeField] protected WeaponDefinition _definition;
 
    [Tooltip("Rotates left/right (yaw). Usually the turret base")]
    [SerializeField] protected Transform _yawPivot;
 
    [Tooltip("Rotates up/down (pitch). Usually the barrel")]
    [SerializeField] protected Transform _pitchPivot;
 
    [Tooltip("Where projectiles leave the barrel")]
    [SerializeField] protected Transform _muzzle;
 
    [Tooltip("Where the gunners camera sits while seated")]
    [SerializeField] protected Transform _cameraAnchor;
 
    [Tooltip("Projectile prefab. Needs NetworkObject + WeaponProjectile")]
    [SerializeField] protected GameObject _projectilePrefab;

    [Tooltip("Tick if the pitch pivot is a SIBLING of the yaw pivot instead of a child")]
    [SerializeField] private bool _pitchPivotIsSibling = false;

    [Tooltip("Empty placed at the barrel's real pivot point. Leave null to rotate around the mesh's own origin.")]
    [SerializeField] private Transform _pitchPivotPoint;
    [Header("Networking")]
    [Tooltip("How many times per second the gunners aim is sent to the server")]
    [SerializeField] private float _aimSendRate = 20f;
 
    [SerializeField] private float _remoteAimSmoothing = 12f;
 
    //SYNCED STATE
    private readonly NetworkVariable<ulong> _occupant = new NetworkVariable<ulong>(NoOccupant);
    private readonly NetworkVariable<Vector2> _aim = new NetworkVariable<Vector2>(Vector2.zero);
    private readonly NetworkVariable<int> _loaded = new NetworkVariable<int>(0);
    private readonly NetworkVariable<float> _ammoMultiplier = new NetworkVariable<float>(1f);
 
    //LOCAL GUNNER STATE
    protected float Pitch;
    protected float Yaw;
    private float _nextAimSend;
    private float _nextShotTime;
 
    public WeaponDefinition Definition => _definition;
    public Transform CameraAnchor => _cameraAnchor;
    public ulong Occupant => _occupant.Value;
    public bool IsOccupied => _occupant.Value != NoOccupant;
    public int Loaded => _loaded.Value;
    public bool NeedsAmmo => _loaded.Value < (_definition != null ? _definition.MagazineCapacity : 1);
 
    //player is currently driving this weapon on this machine
    public PlayerInteractor LocalGunner { get; private set; }
    public bool IsLocalGunner => LocalGunner != null;
 
    //SEAT
 
    [ServerRpc(RequireOwnership = false)]
    public void RequestMountServerRpc(ulong clientId)
    {
        if (IsOccupied) return;
        _occupant.Value = clientId;
    }
 
    [ServerRpc(RequireOwnership = false)]
    public void RequestDismountServerRpc(ulong clientId)
    {
        if (_occupant.Value == clientId) _occupant.Value = NoOccupant;
    }

    private Vector3 _pitchPivotRestPos;

    protected virtual void Awake()
    {
        if (_pitchPivot != null) _pitchPivotRestPos = _pitchPivot.localPosition;
    }
 
    //weaponstation calls this on the mounting client
    public void BeginLocalControl(PlayerInteractor player)
    {
        LocalGunner = player;
        Pitch = _aim.Value.x;
        Yaw = _aim.Value.y;
    }
 
    public void EndLocalControl()
    {
        LocalGunner = null;
    }
 
    //AMMO
 
    public bool AcceptsAmmo(ItemDefinition item)
    {
        if (item == null || _definition == null) return false;
        if (string.IsNullOrEmpty(_definition.AcceptedAmmoTag)) return false;
        return item.AmmoTag == _definition.AcceptedAmmoTag;
    }
 
    [ServerRpc(RequireOwnership = false)]
    public void LoadAmmoServerRpc(float damageMultiplier)
    {
        if (_definition == null || _loaded.Value >= _definition.MagazineCapacity) return;
 
        _loaded.Value = Mathf.Min(_definition.MagazineCapacity, _loaded.Value + Mathf.Max(1, _definition.RoundsPerAmmoItem));
        _ammoMultiplier.Value = Mathf.Max(0.01f, damageMultiplier);
    }
 
    //FIRING
 
    protected bool CanFireNow()
    {
        if (_definition == null || _loaded.Value <= 0) return false;
        return Time.time >= _nextShotTime;
    }
 
    protected void TryFire()
    {
        if (!CanFireNow()) return;
 
        _nextShotTime = _definition.FireMode == FireMode.Automatic
            ? Time.time + 60f / Mathf.Max(1f, _definition.RoundsPerMinute)
            : Time.time + 0.25f;   //small guard against double-taps
 
        FireServerRpc();
 
        //local recoil kick so it feels immediate for the gunner
        Pitch = Mathf.Clamp(Pitch + _definition.Recoil, _definition.PitchMin, _definition.PitchMax);
    }
 
    [ServerRpc(RequireOwnership = false)]
    private void FireServerRpc()
    {
        if (_definition == null || _projectilePrefab == null || _muzzle == null) return;
        if (_loaded.Value <= 0) return;
 
        _loaded.Value -= 1;
 
        Vector3 dir = ApplySpread(_muzzle.forward, _definition.BulletSpread);
 
        GameObject go = Instantiate(_projectilePrefab, _muzzle.position, Quaternion.LookRotation(dir));
        go.GetComponent<NetworkObject>().Spawn();
        go.GetComponent<WeaponProjectile>().Launch(
            dir,
            _definition.ProjectileSpeed,
            _definition.Range,
            _definition.Damage * _ammoMultiplier.Value,
            TrainHealth.Instance != null ? TrainHealth.Instance.Velocity : Vector3.zero);
    }
 
    private static Vector3 ApplySpread(Vector3 forward, float degrees)
    {
        if (degrees <= 0f) return forward;
        Quaternion offset = Quaternion.Euler(
            Random.Range(-degrees, degrees), Random.Range(-degrees, degrees), 0f);
        return offset * forward;
    }
 
    //PER-FRAME
 
    protected virtual void Update()
    {
        if (_definition == null) return;
 
        if (IsLocalGunner)
        {
            HandleGunnerInput(Time.deltaTime);
            ApplyAim(Pitch, Yaw);
            PushAimToServer();
        }
        else
        {
            Vector2 target = _aim.Value;
            float t = 1f - Mathf.Exp(-_remoteAimSmoothing * Time.deltaTime);
            Pitch = Mathf.LerpAngle(Pitch, target.x, t);
            Yaw = Mathf.LerpAngle(Yaw, target.y, t);
            ApplyAim(Pitch, Yaw);
        }
    }
 
    //weapon-specific controls. Set Pitch/Yaw and call TryFire()
    protected abstract void HandleGunnerInput(float dt);
 
    protected void ApplyAim(float pitch, float yaw)
    {
        if (_yawPivot != null) _yawPivot.localRotation = Quaternion.Euler(0f, yaw, 0f);
        if (_pitchPivot != null)
        {
            Quaternion rot = _pitchPivotIsSibling
                ? Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(-pitch, 0f, 0f)
                : Quaternion.Euler(-pitch, 0f, 0f);

            _pitchPivot.localRotation = rot;

            if (_pitchPivotPoint != null)
            {
                Vector3 p = _pitchPivot.parent != null
                    ? _pitchPivot.parent.InverseTransformPoint(_pitchPivotPoint.position)
                    : _pitchPivotPoint.position;

                Vector3 origin = _pitchPivotRestPos;
                _pitchPivot.localPosition = p + rot * (origin - p);
            }
        }
    }
 
    private void PushAimToServer()
    {
        if (Time.time < _nextAimSend) return;
        _nextAimSend = Time.time + 1f / Mathf.Max(1f, _aimSendRate);
        SetAimServerRpc(new Vector2(Pitch, Yaw));
    }
 
    [ServerRpc(RequireOwnership = false)]
    private void SetAimServerRpc(Vector2 aim) => _aim.Value = aim;
 
    //clamps helper for subclasses.
    protected float ClampPitch(float value) =>
        Mathf.Clamp(value, _definition.PitchMin, _definition.PitchMax);
 
    protected float ClampYaw(float value) =>
        _definition.LimitYaw ? Mathf.Clamp(value, _definition.YawMin, _definition.YawMax) : value;
}
