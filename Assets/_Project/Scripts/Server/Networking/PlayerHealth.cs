using System.Collections;
using Unity.Netcode;
using UnityEngine;
 
public sealed class PlayerHealth : NetworkBehaviour, IDamageable
{
    public const ulong EnvironmentAttacker = ulong.MaxValue;
 
    public static PlayerHealth Local { get; private set; }
 
    [SerializeField] private int _maxHealth = 100;
 
    [Tooltip("Seconds until a dead player respawns. 0 = no respawn: they spectate until the level ends.")]
    [SerializeField] private float _respawnDelay = 10f;
 
    [Tooltip("Can players hurt each other with their weapons?")]
    [SerializeField] private bool _friendlyFire = false;
 
    [Tooltip("Falling this far below the train kills the player.")]
    [SerializeField] private float _fallDeathDepth = 8f;

    [Tooltip("Standing on anything that isn't the train (ground, tracks) for this long kills the player.")]
    [SerializeField] private float _offTrainDeathTime = 0.2f;

    private float _offTrainTimer;
 
    private readonly NetworkVariable<int> _health = new NetworkVariable<int>(100);
    private readonly NetworkVariable<bool> _dead = new NetworkVariable<bool>(false);
    private readonly NetworkVariable<double> _respawnAt = new NetworkVariable<double>(-1.0);
 
    private FirstPersonController _controller;
    private PlayerCarriageAttachment _attachment;
    private PlayerInventory _inventory;
    private PlayerInteractor _interactor;
    private CharacterController _body;
 
    private CapsuleCollider _hitbox;
    private float _standHeight;
    private Vector3 _standCenter;
 
    private Renderer[] _renderers;
    private bool[] _rendererWasOn;
    private bool _fallReported;
 
    public int Health => _health.Value;
    public int MaxHealth => _maxHealth;
    public bool IsDead => _dead.Value;
 
    public double RespawnAt => _respawnAt.Value;
 
    private void Awake()
    {
        _controller = GetComponent<FirstPersonController>();
        _attachment = GetComponent<PlayerCarriageAttachment>();
        _inventory = GetComponentInChildren<PlayerInventory>();
        _interactor = GetComponentInChildren<PlayerInteractor>();
        _body = GetComponent<CharacterController>();
 
        _hitbox = gameObject.AddComponent<CapsuleCollider>();
        _hitbox.isTrigger = true;
        _standHeight = _body.height;
        _standCenter = _body.center;
        _hitbox.radius = _body.radius;
        _hitbox.height = _standHeight;
        _hitbox.center = _standCenter;
 
        _renderers = GetComponentsInChildren<Renderer>(true);
        _rendererWasOn = new bool[_renderers.Length];
    }
 
    public override void OnNetworkSpawn()
    {
        if (IsOwner) Local = this;
        if (IsServer) _health.Value = _maxHealth;
 
        _dead.OnValueChanged += OnDeadChanged;
        if (_dead.Value) ApplyDead(true);
    }
 
    public override void OnNetworkDespawn()
    {
        _dead.OnValueChanged -= OnDeadChanged;
        if (Local == this) Local = null;
    }
 
    private void LateUpdate()
    {
        UpdateHitbox();
 
        if (IsOwner) CheckFellOff();
    }
 
    private void UpdateHitbox()
    {
        if (_hitbox == null || _controller == null) return;
 
        float height = _controller.IsCrouched ? _standHeight * _controller.crouchHeightFactor : _standHeight;
        _hitbox.height = height;
        _hitbox.center = _standCenter - new Vector3(0f, (_standHeight - height) * 0.5f, 0f);
    }

    private void CheckFellOff()
    {
        if (_dead.Value || _fallReported || _attachment == null || !_attachment.IsRiding) return;
        if (TrainHealth.Instance == null || TrainHealth.Instance.CarryReference == null) return;

        bool fellOff = transform.position.y < TrainHealth.Instance.CarryReference.position.y - _fallDeathDepth;
        if (!fellOff)
        {
            _offTrainTimer = IsStandingOffTrain() ? _offTrainTimer + Time.deltaTime : 0f;
            fellOff = _offTrainTimer >= _offTrainDeathTime;
        }
        if (!fellOff) return;

        _fallReported = true;
        _offTrainTimer = 0f;
        KillSelfServerRpc();
    }

    private bool IsStandingOffTrain()
    {
        float halfHeight = _body.height * 0.5f * transform.lossyScale.y;
        Vector3 center = transform.TransformPoint(_body.center);

        if (!Physics.Raycast(center, Vector3.down, out RaycastHit hit, halfHeight + 0.3f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            return false;

        if (hit.collider.GetComponentInParent<CharacterController>() != null) return false;
        return !IsTrain(hit.collider.transform);
    }

    private static bool IsTrain(Transform t)
    {
        if (TrainHealth.Instance != null && t.IsChildOf(TrainHealth.Instance.transform)) return true;
        return Trainbuilder.Instance != null && Trainbuilder.Instance.CarIndexOf(t) >= 0;
    }
 
    // ----- damage (server) ---------------------------------------------------
 
    public void ApplyDamage(float amount, Vector3 hitPoint, ulong attackerClientId)
    {
        if (DebugCheats.PlayerGodMode) return;
        if (!IsServer || !IsSpawned || _dead.Value) return;
 
        bool fromPlayer = attackerClientId != EnvironmentAttacker && NetworkManager.ConnectedClients.ContainsKey(attackerClientId);
        if (fromPlayer && !_friendlyFire) return;
 
        int damage = Mathf.Max(1, Mathf.RoundToInt(amount));
        _health.Value = Mathf.Max(0, _health.Value - damage);
 
        if (_health.Value == 0) Kill(fromPlayer ? "shot by a teammate" : "killed");
    }
 
    public void Kill(string cause)
    {
        if (!IsServer || !IsSpawned || _dead.Value) return;
 
        _health.Value = 0;
        _dead.Value = true;
        if (TryGetComponent(out StatusEffectReceiver status)) status.ClearAll();
        _respawnAt.Value = _respawnDelay > 0f ? NetworkManager.ServerTime.Time + _respawnDelay : -1.0;
        Debug.Log($"Player {OwnerClientId} died: {cause}");
 
        if (_respawnDelay > 0f) StartCoroutine(RespawnAfterDelay());
    }
 
    [ServerRpc]
    private void KillSelfServerRpc() => Kill("fell off the train");
 
    private IEnumerator RespawnAfterDelay()
    {
        yield return new WaitForSeconds(_respawnDelay);
 
        if (!IsSpawned || !_dead.Value) yield break;
        if (TrainHealth.Instance != null && TrainHealth.Instance.IsDead) yield break;
 
        _health.Value = _maxHealth;
        _respawnAt.Value = -1.0;
        _dead.Value = false;
    }
 
    // ----- death and respawn (every client) ------------------------------------
 
    private void OnDeadChanged(bool wasDead, bool isDead) => ApplyDead(isDead);
 
    private void ApplyDead(bool dead)
    {
        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] == null) continue;
            if (dead)
            {
                _rendererWasOn[i] = _renderers[i].enabled;
                _renderers[i].enabled = false;
            }
            else
            {
                _renderers[i].enabled = _rendererWasOn[i];
            }
        }
        if (_hitbox != null) _hitbox.enabled = !dead;
 
        if (!IsOwner) return;
 
        if (dead)
        {
            if (MinigameHost.Instance != null && MinigameHost.Instance.IsRunning)
                MinigameHost.Instance.CancelActive();

            foreach (WeaponStation station in FindObjectsByType<WeaponStation>(FindObjectsSortMode.None))
                station.ForceDismount();
 
            if (_inventory != null && _inventory.HasItem) _inventory.Drop();

            if (_fallReported && _attachment != null) _attachment.Respawn();
        }
 
        if (_interactor != null) _interactor.Busy = dead;
        if (_controller != null) _controller.Dead = dead;
 
        if (!dead)
        {
            _fallReported = false;
            if (_attachment != null) _attachment.Respawn();
        }
    }

    // ----- debug (Inspector: menu on this component, Play mode only) ---------

    [ContextMenu("Debug: Kill")]
    public void DebugKill()
    {
        if (IsSpawned) DebugKillServerRpc();
    }

    [ContextMenu("Debug: Damage 25")]
    private void DebugDamage()
    {
        if (IsSpawned) DebugDamageServerRpc(25);
    }

    [ServerRpc(RequireOwnership = false)]
    private void DebugKillServerRpc() => Kill("debug kill");

    [ServerRpc(RequireOwnership = false)]
    private void DebugDamageServerRpc(int amount) => ApplyDamage(amount, transform.position, EnvironmentAttacker);
}