using System;
using UnityEngine;

[Serializable]
public class RankTier
{
    [SerializeField] private string displayName;
    [SerializeField] private int minSuccessfulMissions;
    [SerializeField] private Sprite insignia;

    public string DisplayName
    {
        get { return displayName; }
    }

    public int MinSuccessfulMissions
    {
        get { return minSuccessfulMissions; }
    }

    public Sprite Insignia
    {
        get { return insignia; }
    }
}
