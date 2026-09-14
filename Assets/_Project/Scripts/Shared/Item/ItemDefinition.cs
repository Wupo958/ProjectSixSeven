using UnityEngine;
 
//designer-authored item. Create via Assets > Create > PCCMT > Item Definition
//make one asset per item: Hammer, Light Ammo, Heavy Ammo, ...
[CreateAssetMenu(menuName = "PCCMT/Item Definition", fileName = "Item_")]
public sealed class ItemDefinition : ScriptableObject
{
    [Header("Display")]
    public string DisplayName = "Item";
 
    [Tooltip("Visual spawned in the player's hand while held. Visual only: no colliders, no rigidbody")]
    public GameObject HeldPrefab;
 
    [Header("Behaviour")]
    [Tooltip("This item can repair broken damage points (the hammer)")]
    public bool CanRepair;
 
    [Tooltip("Item is destroyed after a successful use")]
    public bool ConsumeOnUse;
 
    [Tooltip("Free-form tag for later systems, e.g. \"LightAmmo\", \"HeavyAmmo\"")]
    public string AmmoTag = "";

    [Tooltip("Multiplies the weapon's base damage when this ammo is loaded")]
    public float AmmoDamageMultiplier = 1f;
}
 
