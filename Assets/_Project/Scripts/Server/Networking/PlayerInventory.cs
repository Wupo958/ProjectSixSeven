using Unity.Netcode;
using UnityEngine;
 
public sealed class PlayerInventory : NetworkBehaviour
{
    [Tooltip("Registry asset listing every item. Same asset on every prefab")]
    [SerializeField] private ItemRegistry _registry;
 
    [Tooltip("Empty transform on the player (usually under the camera) where held items appear")]
    [SerializeField] private Transform _handAnchor;
 
    private readonly NetworkVariable<int> _heldId =
        new NetworkVariable<int>(ItemRegistry.Empty,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
 
    private GameObject _handVisual;
 
    public ItemDefinition Held => _registry != null ? _registry.Get(_heldId.Value) : null;
    public bool HasItem => Held != null;
 
    private void Awake()
    {
        if (_registry != null) _registry.MakeActive();
    }
 
    public override void OnNetworkSpawn()
    {
        _heldId.OnValueChanged += OnHeldChanged;
        RebuildHandVisual(_heldId.Value);   //for late joiners (optional)
    }
 
    public override void OnNetworkDespawn()
    {
        _heldId.OnValueChanged -= OnHeldChanged;
    }
 
    //OWNER-SIDED
 
    public bool TryPickUp(ItemDefinition item)
    {
        if (!IsOwner || item == null || HasItem) return false;
 
        int id = _registry != null ? _registry.IdOf(item) : ItemRegistry.Empty;
        if (id == ItemRegistry.Empty)
        {
            Debug.LogWarning($"{item.name} is not in the ItemRegistry.", this);
            return false;
        }
 
        _heldId.Value = id;
        return true;
    }
 
    //currently dropping an item destroys it
    public void Drop()
    {
        if (!IsOwner || !HasItem) return;
        _heldId.Value = ItemRegistry.Empty;
    }
 
    //Called after successful usage & removes consumable
    public void NotifyUsed()
    {
        if (!IsOwner) return;
        ItemDefinition item = Held;
        if (item != null && item.ConsumeOnUse) _heldId.Value = ItemRegistry.Empty;
    }
 
    //VISUALS
 
    private void OnHeldChanged(int oldId, int newId) => RebuildHandVisual(newId);
 
    private void RebuildHandVisual(int id)
    {
        if (_handVisual != null) Destroy(_handVisual);
        _handVisual = null;
 
        ItemDefinition item = _registry != null ? _registry.Get(id) : null;
        if (item == null || item.HeldPrefab == null || _handAnchor == null) return;
 
        _handVisual = Instantiate(item.HeldPrefab, _handAnchor);
        _handVisual.transform.localPosition = Vector3.zero;
        _handVisual.transform.localRotation = Quaternion.identity;
    }

    public void ConsumeHeld()
    {
        if (!IsOwner) return;
        _heldId.Value = ItemRegistry.Empty;
    }
}
