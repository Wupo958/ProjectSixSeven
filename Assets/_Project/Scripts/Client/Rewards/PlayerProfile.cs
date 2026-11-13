using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

[Serializable]
public class PlayerProfile
{
    [SerializeField, JsonProperty] private int saveVersion = 1;
    [SerializeField, JsonProperty] private string displayName = "";
    [SerializeField, JsonProperty] private int successfulMissions;
    [SerializeField, JsonProperty] private List<string> unlockedMedalIds = new List<string>();

    [JsonIgnore]
    public int SaveVersion
    {
        get { return saveVersion; }
    }

    [JsonIgnore]
    public string DisplayName
    {
        get { return displayName; }
        set { displayName = value; }
    }

    [JsonIgnore]
    public int SuccessfulMissions
    {
        get { return successfulMissions; }
    }

    [JsonIgnore]
    public List<string> UnlockedMedalIds
    {
        get { return unlockedMedalIds; }
    }

    public void AddSuccessfulMission()
    {
        successfulMissions++;
    }
}
