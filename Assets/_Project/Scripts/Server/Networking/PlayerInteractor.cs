using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(100)] 
public sealed class PlayerInteractor : NetworkBehaviour
{
    [Header("Raycast")]
    [SerializeField] private Transform _rayOrigin;      //the player camera
    [SerializeField] private float _range = 3f;
    [Tooltip("Thickness of the aim ray. A little width keeps small targets from flickering at their edges.")]
    [SerializeField] private float _aimRadius = 0.1f;
    [SerializeField] private LayerMask _mask = ~0;
 
    [Header("Input")]
    [SerializeField] private Key _interactKey = Key.E;
    [SerializeField] private Key _dropKey = Key.Q;
 
    [Header("Dropping & throwing")]
    [Tooltip("Tap [Q] shorter than this to drop; hold longer to aim and throw.")]
    [SerializeField] private float _throwHoldThreshold = 0.2f;
 
    [Tooltip("Seconds of holding (after the threshold) until the throw is at full power.")]
    [SerializeField] private float _fullChargeTime = 1f;
 
    [Tooltip("Throw speed at full power in m/s, for items at or below Reference Weight. 10 m/s throws roughly 10 m. This is the maximum.")]
    [SerializeField] private float _maxThrowSpeed = 10f;
 
    [Tooltip("Power of the quickest possible throw, as a fraction of full power.")]
    [Range(0f, 1f)]
    [SerializeField] private float _minThrowPower = 0.2f;
 
    [Tooltip("Items heavier than this (kg) are thrown proportionally less far.")]
    [SerializeField] private float _referenceWeight = 1f;
 
    [Tooltip("How far in front of your eyes items are dropped, in metres.")]
    [SerializeField] private float _dropDistance = 0.5f;
 
    [Header("Catching")]
    [Tooltip("Flying items within this distance of your eyes can be caught with [E].")]
    [SerializeField] private float _catchRange = 1.5f;

    [Header("UI")]
    [Tooltip("On-screen prompt label. Optional but very useful.")]
    [SerializeField] private TMP_Text _promptLabel;
 
    private PlayerInventory _inventory;
    private Interactable _current;
    private WorldItem _catchable;
    private float _dropPressedAt = -1f;

    public Interactable Current => _current;
    public PlayerInventory Inventory => _inventory;
    public Transform RayOrigin => _rayOrigin;

    //0..1 while aiming a throw, for UI
    public float ThrowCharge { get; private set; }
    public bool IsAimingThrow => _dropPressedAt >= 0f && Time.time - _dropPressedAt > _throwHoldThreshold;
 
    //true while a minigame is open, so we do not interact through the UI
    public bool Busy { get; set; }
 
    private void Awake()
    {
        _inventory = GetComponent<PlayerInventory>();
        if (_rayOrigin == null && Camera.main != null) _rayOrigin = Camera.main.transform;
        if (_promptLabel == null) _promptLabel = PromptLabel.Instance;
    }
 
    private void LateUpdate()
    {
        if (!IsOwner) return;
 
        if (Busy)
        {
            CancelThrow();
            SetPrompt(string.Empty);
            return;
        }
 
        _current = FindTarget();
        _catchable = _rayOrigin != null ? WorldItem.FindCatchable(_rayOrigin.position, _catchRange) : null;
 
        Keyboard kb = Keyboard.current;
        if (kb != null)
        {
            HandleInteract(kb);
            HandleDropAndThrow(kb);
        }
 
        UpdatePrompt();
    }

    private void HandleInteract(Keyboard kb)
    {
        if (!kb[_interactKey].wasPressedThisFrame) return;
        if (MinigameHost.Instance != null && MinigameHost.Instance.FinishedThisFrame) return;
 
        //catching is time-critical, so a flying item in reach comes first
        if (_catchable != null)
        {
            _catchable.RequestPickUp();
            return;
        }
 
        if (_current != null && _current.CanInteract(this)) _current.Interact(this);
    }

    private void HandleDropAndThrow(Keyboard kb)
    {
        if (_inventory == null || !_inventory.HasItem)
        {
            CancelThrow();
            return;
        }
 
        if (kb[_dropKey].wasPressedThisFrame) _dropPressedAt = Time.time;
        if (_dropPressedAt < 0f) return;
 
        float held = Time.time - _dropPressedAt;
        ThrowCharge = held > _throwHoldThreshold
            ? Mathf.Clamp01((held - _throwHoldThreshold) / Mathf.Max(0.01f, _fullChargeTime))
            : 0f;
 
        if (!kb[_dropKey].wasReleasedThisFrame) return;
 
        Vector3 position = DropPosition();
        if (held <= _throwHoldThreshold) _inventory.DropHeld(position, Vector3.zero);
        else _inventory.DropHeld(position, ThrowVelocity(_inventory.Held, ThrowCharge));
 
        CancelThrow();
    }
 
    private void CancelThrow()
    {
        _dropPressedAt = -1f;
        ThrowCharge = 0f;
    }
    
    private Vector3 ThrowVelocity(ItemDefinition item, float charge)
    {
        float weight = item != null ? Mathf.Max(0.01f, item.Weight) : 1f;
        float weightFactor = Mathf.Clamp01(_referenceWeight / weight);
        float power = Mathf.Lerp(_minThrowPower, 1f, charge);
        return _rayOrigin.forward * (_maxThrowSpeed * power * weightFactor);
    }

    private Vector3 DropPosition()
    {
        Vector3 origin = _rayOrigin.position;
        Vector3 forward = _rayOrigin.forward;
        float distance = _dropDistance;
 
        //not through a wall
        if (Physics.Raycast(origin, forward, out RaycastHit hit, _dropDistance + 0.15f, ~0, QueryTriggerInteraction.Ignore)
            && !hit.collider.transform.IsChildOf(transform.root))
        {
            distance = Mathf.Max(0f, hit.distance - 0.15f);
        }
 
        return origin + forward * distance;
    }
 
    private Interactable FindTarget()
    {
        if (_rayOrigin == null) return null;

        Physics.SyncTransforms();

        return Physics.SphereCast(_rayOrigin.position, _aimRadius, _rayOrigin.forward, out RaycastHit hit, _range, _mask, QueryTriggerInteraction.Collide) ? hit.collider.GetComponentInParent<Interactable>() : null;
    }
 
    private void UpdatePrompt()
    {
        string text;
 
        if (IsAimingThrow && _inventory != null && _inventory.HasItem)
        {
            text = $"Throwing {_inventory.Held.DisplayName}  {Mathf.RoundToInt(ThrowCharge * 100f)}%";
        }
        else if (_catchable != null)
        {
            ItemDefinition def = _catchable.Definition;
            text = $"[{_interactKey}] Catch {(def != null ? def.DisplayName : "item")}";
        }
        else
        {
            text = _current != null ? _current.GetPrompt(this) : string.Empty;
 
            if (_current == null && _inventory != null && _inventory.HasItem)
                text = $"[{_dropKey}] Drop {_inventory.Held.DisplayName}   Hold [{_dropKey}] to throw";
        }
 
        SetPrompt(text);
    }
 
    private void SetPrompt(string text)
    {
        if (_promptLabel == null) _promptLabel = PromptLabel.Instance;
        if (_promptLabel != null) _promptLabel.text = text;
    }

}
 
