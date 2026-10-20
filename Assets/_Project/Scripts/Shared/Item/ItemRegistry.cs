using System.Collections.Generic;
using UnityEngine;
 
//maps items to network-safe ids. Only the index travels over the network,
//so items themselves never need to be networked objects.
//create one asset (Assets > Create > PCCMT > Item Registry) and list every item.
[CreateAssetMenu(menuName = "PCCMT/Item Registry", fileName = "ItemRegistry")]
public sealed class ItemRegistry : ScriptableObject
{
    public const int Empty = -1;
 
    public static ItemRegistry Active { get; private set; }
 
    [Tooltip("Every item in the game. Order defines the network id, so avoid reordering " +
             "once you start testing builds.")]
    public List<ItemDefinition> Items = new List<ItemDefinition>();
 
    public void MakeActive() => Active = this;
 
    public int IdOf(ItemDefinition item)
    {
        int index = Items.IndexOf(item);
        return index >= 0 ? index : Empty;
    }
 
    public ItemDefinition Get(int id)
    {
        return id >= 0 && id < Items.Count ? Items[id] : null;
    }
}
