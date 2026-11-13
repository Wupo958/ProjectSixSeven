using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.CloudSave;
using Unity.Services.CloudSave.Models;
using UnityEngine;

public static class ProfileService
{
    private static string profileKey = "profile";

    public static event Action<string> MedalUnlocked;
    public static event Action ProfileChanged;

    public static PlayerProfile Current { get; private set; }
    public static bool IsLoaded { get; private set; }

    public static async Task LoadAsync()
    {
        HashSet<string> keys = new HashSet<string>();
        keys.Add(profileKey);

        try
        {
            Dictionary<string, Item> savedData = await CloudSaveService.Instance.Data.Player.LoadAsync(keys);

            Item savedProfile;
            if (savedData.TryGetValue(profileKey, out savedProfile))
            {
                Current = savedProfile.Value.GetAs<PlayerProfile>();
            }
            else
            {
                Current = new PlayerProfile();
            }

            IsLoaded = true;
            Debug.Log("Profile loaded: " + Current.SuccessfulMissions + " missions, " + Current.UnlockedMedalIds.Count + " medals");
            RaiseProfileChanged();
        }
        catch (Exception e)
        {
            Debug.LogError("Loading player profile failed: " + e);
        }
    }

    public static void Unload()
    {
        Current = null;
        IsLoaded = false;
        RaiseProfileChanged();
    }

    public static async Task SaveAsync()
    {
        if (!IsLoaded)
        {
            Debug.LogWarning("Profile was never loaded, skipping save so the cloud copy is not overwritten.");
            return;
        }

        Dictionary<string, object> data = new Dictionary<string, object>();
        data.Add(profileKey, Current);

        try
        {
            await CloudSaveService.Instance.Data.Player.SaveAsync(data);
        }
        catch (Exception e)
        {
            Debug.LogError("Saving player profile failed: " + e);
        }
    }

    public static async Task SetDisplayName(string displayName)
    {
        if (Current.DisplayName == displayName)
        {
            return;
        }

        Current.DisplayName = displayName;
        RaiseProfileChanged();
        await SaveAsync();
    }

    public static async Task AddSuccessfulMission()
    {
        Current.AddSuccessfulMission();
        RaiseProfileChanged();
        await SaveAsync();
    }

    public static bool HasMedal(string medalId)
    {
        return Current.UnlockedMedalIds.Contains(medalId);
    }

    public static async Task UnlockMedal(string medalId)
    {
        if (HasMedal(medalId))
        {
            return;
        }

        Current.UnlockedMedalIds.Add(medalId);
        Debug.Log("Medal unlocked: " + medalId);

        if (MedalUnlocked != null)
        {
            MedalUnlocked.Invoke(medalId);
        }

        RaiseProfileChanged();
        await SaveAsync();
    }

    private static void RaiseProfileChanged()
    {
        if (ProfileChanged != null)
        {
            ProfileChanged.Invoke();
        }
    }
}
