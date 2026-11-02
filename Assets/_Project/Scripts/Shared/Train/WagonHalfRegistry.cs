using System.Collections.Generic;
using UnityEngine;
 
[CreateAssetMenu(menuName = "PCCMT/Wagon Half Registry", fileName = "WagonHalfRegistry")]
public sealed class WagonHalfRegistry : ScriptableObject
{
    public const int Empty = -1;
 
    [Tooltip("Every buildable wagon half. Order defines the network id, so do not " +
             "reorder once builds are out.")]
    public List<Wagonhalf> Halves = new List<Wagonhalf>();
 
    public int IdOf(Wagonhalf half)
    {
        int i = Halves.IndexOf(half);
        return i >= 0 ? i : Empty;
    }
 
    public Wagonhalf Get(int id) => id >= 0 && id < Halves.Count ? Halves[id] : null;
 
    public string NameOf(int id)
    {
        Wagonhalf h = Get(id);
        return h != null ? h.DisplayName : "Empty";
    }
}
