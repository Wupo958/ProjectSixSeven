using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
 
public sealed class PlayerInteractor : NetworkBehaviour
{
    [Header("Raycast")]
    [SerializeField] private Transform _rayOrigin;      //the player camera
    [SerializeField] private float _range = 3f;
    [SerializeField] private LayerMask _mask = ~0;
 
    [Header("Input")]
    [SerializeField] private Key _interactKey = Key.E;
    [SerializeField] private Key _dropKey = Key.G;
 
    [Header("UI")]
    [Tooltip("On-screen prompt label. Optional but very useful.")]
    [SerializeField] private TMP_Text _promptLabel;
 
    private PlayerInventory _inventory;
    private Interactable _current;
 
    public PlayerInventory Inventory => _inventory;
    public Transform RayOrigin => _rayOrigin;
 
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
            SetPrompt(string.Empty);
            return;
        }
 
        _current = FindTarget();
        UpdatePrompt();
 
        if (Keyboard.current == null) return;
 
        if (_current != null && Keyboard.current[_interactKey].wasPressedThisFrame
            && _current.CanInteract(this))
        {
            _current.Interact(this);
        }
 
        if (Keyboard.current[_dropKey].wasPressedThisFrame && _inventory != null)
        {
            _inventory.Drop();
        }
    }
 
    private Interactable FindTarget()
    {
        if (_rayOrigin == null) return null;
 
        if (Physics.Raycast(_rayOrigin.position, _rayOrigin.forward, out RaycastHit hit, _range, _mask, QueryTriggerInteraction.Collide))
        {
            return hit.collider.GetComponentInParent<Interactable>();
        }
        return null;
    }
 
    private void UpdatePrompt()
    {
        string text = _current != null ? _current.GetPrompt(this) : string.Empty;
 
        if (_current == null && _inventory != null && _inventory.HasItem)
            text = $"[{_dropKey}] Drop {_inventory.Held.DisplayName}";
 
        SetPrompt(text);
    }
 
    private void SetPrompt(string text)
    {
        if (_promptLabel == null) _promptLabel = PromptLabel.Instance;
        if (_promptLabel != null) _promptLabel.text = text;
    }

}
 
