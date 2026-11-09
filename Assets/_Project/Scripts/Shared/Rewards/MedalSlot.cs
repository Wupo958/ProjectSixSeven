using System;
using UnityEngine;

[Serializable]
public class MedalSlot
{
    [SerializeField] private MedalDefinition medal;
    [SerializeField] private GameObject visual;

    public MedalDefinition Medal
    {
        get { return medal; }
    }

    public GameObject Visual
    {
        get { return visual; }
    }
}
