using UnityEngine;
 
//a crate that hands out an unlimited supply of one item.
//put it on the box mesh (needs a collider) and pick the item in the Inspector.
public sealed class ItemSourceBox : Interactable
{
    [SerializeField] private ItemDefinition _item;
 
    [Tooltip("Verb shown in the prompt")]
    [SerializeField] private string _verb = "Take";
 
    public override bool CanInteract(PlayerInteractor player)
    {
        return _item != null && player.Inventory != null && !player.Inventory.HasItem;
    }
 
    public override string GetPrompt(PlayerInteractor player)
    {
        if (_item == null) return string.Empty;
 
        if (player.Inventory != null && player.Inventory.HasItem)
            return $"Hands full ({player.Inventory.Held.DisplayName})";
 
        return $"[E] {_verb} {_item.DisplayName}";
    }
 
    public override void Interact(PlayerInteractor player)
    {
        player.Inventory?.TryPickUp(_item);
    }
}
