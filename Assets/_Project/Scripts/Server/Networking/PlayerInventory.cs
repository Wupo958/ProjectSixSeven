using Unity.Netcode;
using UnityEngine;
 
public sealed class PlayerInventory : NetworkBehaviour
{
    [Tooltip("Registry asset listing every item. Same asset on every prefab")]
    [SerializeField] private ItemRegistry _registry;
 
    [Tooltip("Empty transform on the player (usually under the camera) where held items appear")]
    [SerializeField] private Transform _handAnchor;
 
    private readonly NetworkVariable<int> _heldId = new NetworkVariable<int>(ItemRegistry.Empty, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
 
    private GameObject _handVisual;
 
    public ItemDefinition Held => _registry != null ? _registry.Get(_heldId.Value) : null;
    public bool HasItem => Held != null;

    private Vector3 HandPosition => _handAnchor != null ? _handAnchor.position : transform.position + Vector3.up * 0.5f;
 
    private void Awake()
    {
        if (_registry != null) _registry.MakeActive();
    }
 
    public override void OnNetworkSpawn()
    {
        _heldId.OnValueChanged += OnHeldChanged;
        RebuildHandVisual(_heldId.Value);   //for late joiners
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
 
    public void Drop() => DropHeld(HandPosition, Vector3.zero);

    public void DropHeld(Vector3 worldPosition, Vector3 velocityRelativeToTrain)
    {
        if (!IsOwner || !HasItem) return;
 
        int id = _heldId.Value;
        _heldId.Value = ItemRegistry.Empty;
 
        Transform frame = WorldItem.TrainFrame;
        Vector3 localPos = frame != null ? frame.InverseTransformPoint(worldPosition) : worldPosition;
        Vector3 localVel = frame != null ? frame.InverseTransformDirection(velocityRelativeToTrain) : velocityRelativeToTrain;
 
        SpawnWorldItemServerRpc(id, localPos, localVel);
    }

    [ServerRpc]
    private void SpawnWorldItemServerRpc(int id, Vector3 trainLocalPos, Vector3 trainLocalVel)
    {
        ItemDefinition def = _registry != null ? _registry.Get(id) : null;
        if (def == null) return;
 
        if (def.WorldPrefab == null)
        {
            Debug.LogWarning($"{def.name} has no World Prefab, so it was destroyed instead of dropped.", def);
            return;
        }
 
        Transform frame = WorldItem.TrainFrame;
        Vector3 position = frame != null ? frame.TransformPoint(trainLocalPos) : trainLocalPos;
        Vector3 velocity = (frame != null ? frame.TransformDirection(trainLocalVel) : trainLocalVel) + WorldItem.TrainVelocity;
 
        WorldItem.SpawnFlying(def.WorldPrefab, id, position, velocity);
    }
 
    [ClientRpc]
    public void GrantItemClientRpc(int id, ClientRpcParams rpcParams = default)
    {
        if (!IsOwner) return;
 
        if (HasItem) Drop();
        _heldId.Value = id;
    }
 
    public void NotifyUsed()
    {
        if (!IsOwner) return;
        ItemDefinition item = Held;
        if (item != null && item.ConsumeOnUse) _heldId.Value = ItemRegistry.Empty;
    }

    public void ConsumeHeld()
    {
        if (!IsOwner) return;
        _heldId.Value = ItemRegistry.Empty;
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
}
