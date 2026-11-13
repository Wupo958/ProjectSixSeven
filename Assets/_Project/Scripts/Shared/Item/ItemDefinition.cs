using UnityEngine;
 
public enum ItemType
{
    Ammo = 0,        //fired by weapons, one tag per weapon
    Fuel = 1,        //keeps the Lokomotive at speed
    SpareParts = 2,  //used up repairing defects
    Tool = 3,        //used for maintenance, kept afterwards
}

[CreateAssetMenu(menuName = "PCCMT/Item Definition", fileName = "Item_")]
public sealed class ItemDefinition : ScriptableObject
{
    [Header("Display")]
    public string DisplayName = "Item";

    [Header("World")]
    [Tooltip("Placed in the world when the item is dropped or thrown. Needs NetworkObject, WorldItemPickup, a collider and a Rigidbody, and must be in the NetworkManager's prefab list. Pivot at the bottom.")]
    public GameObject WorldPrefab;

    [Tooltip("In kg. Heavier items are thrown less far.")]
    public float Weight = 1f;
 
    [Tooltip("Visual spawned in the player's hand while held. Visual only: no colliders, no rigidbody")]
    public GameObject HeldPrefab;

    [Header("Type")]
    [Tooltip("GDD item category. Changing it fills in the usual values below; you can still override them.")]
    public ItemType Type = ItemType.SpareParts;
 
    [Tooltip("Used up when used: loaded into a weapon, fed to the Lok, spent on a repair. Tools usually aren't.")]
    public bool ConsumeOnUse = true;

    [Tooltip("Storing it in a wagon makes that wagon explosive (Ammo, Fuel). Data only for now.")]
    public bool ExplosiveWhenStored;

    [Tooltip("Can break (some spare parts). Data only for now.")]
    public bool Breakable;

    [Header("Ammo only")]
    [Tooltip("Which weapon this fits, e.g. \"LightAmmo\" or \"HeavyAmmo\". Must match the weapon's Accepted Ammo Tag.")]
    public string AmmoTag = "";

    [Tooltip("Ammo type (Explosive, Armor Piercing, ...) as a multiplier on the weapon's base damage.")]
    public float AmmoDamageMultiplier = 1f;

    //remembers which type the defaults were last filled in for, so changing the type applies them once without undoing later manual tweaks.
    [SerializeField, HideInInspector] private ItemType _defaultsAppliedFor = (ItemType)(-1);

    private void OnValidate()
    {
        if (_defaultsAppliedFor == Type) return;
        _defaultsAppliedFor = Type;
 
        switch (Type)
        {
            case ItemType.Ammo:
            case ItemType.Fuel:
                ConsumeOnUse = true;
                ExplosiveWhenStored = true;
                Breakable = false;
                break;
 
            case ItemType.SpareParts:
                ConsumeOnUse = true;
                ExplosiveWhenStored = false;
                break;
 
            case ItemType.Tool:
                ConsumeOnUse = false;
                ExplosiveWhenStored = false;
                Breakable = false;
                break;
        }
    }

}
 
