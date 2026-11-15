using UnityEngine;
 
[RequireComponent(typeof(WorldItem))]
public sealed class WorldItemPickup : Interactable
{
    private WorldItem _item;
 
    private void Awake()
    {
        _item = GetComponent<WorldItem>();
    }
 
    public override bool CanInteract(PlayerInteractor player)
    {
        return _item != null && _item.IsSpawned && !_item.IsFlying;
    }
 
    public override string GetPrompt(PlayerInteractor player)
    {
        if (!CanInteract(player)) return string.Empty;
 
        ItemDefinition def = _item.Definition;
        string name = def != null ? def.DisplayName : "item";
 
        if (player.Inventory != null && player.Inventory.HasItem)
            return $"[E] Pick up {name} (drops {player.Inventory.Held.DisplayName})";
 
        return $"[E] Pick up {name}";
    }
 
    public override void Interact(PlayerInteractor player)
    {
        _item.RequestPickUp();
    }
}
 