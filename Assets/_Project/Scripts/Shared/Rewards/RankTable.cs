using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Rewards/Rank Table")]
public class RankTable : ScriptableObject
{
    [SerializeField] private List<RankTier> tiers = new List<RankTier>();

    public RankTier GetRank(int successfulMissions)
    {
        RankTier bestTier = tiers[0];
        foreach (RankTier tier in tiers)
        {
            bool isReached = successfulMissions >= tier.MinSuccessfulMissions;
            bool isHigher = tier.MinSuccessfulMissions > bestTier.MinSuccessfulMissions;
            if (isReached && isHigher)
            {
                bestTier = tier;
            }
        }

        return bestTier;
    }
}
