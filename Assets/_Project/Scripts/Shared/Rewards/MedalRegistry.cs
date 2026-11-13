using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Rewards/Medal Registry")]
public class MedalRegistry : ScriptableObject
{
    [SerializeField] private List<MedalDefinition> medals = new List<MedalDefinition>();

    public List<MedalDefinition> Medals
    {
        get { return medals; }
    }

    public MedalDefinition Find(string medalId)
    {
        foreach (MedalDefinition medal in medals)
        {
            if (medal.Id == medalId)
            {
                return medal;
            }
        }

        return null;
    }
}
