using System.Collections.Generic;
using TMPro;
using Unity.Services.Multiplayer;
using UnityEngine;

public class LobbyPlayerListUI : MonoBehaviour
{
    [SerializeField] private TMP_Text playerListText;

    private ISession session;

    public static string DisplayNameKey
    {
        get { return "displayName"; }
    }

    public static Dictionary<string, PlayerProperty> CreateLocalPlayerProperties()
    {
        Dictionary<string, PlayerProperty> properties = new Dictionary<string, PlayerProperty>();
        properties.Add(DisplayNameKey, new PlayerProperty(ProfileService.Current.DisplayName, VisibilityPropertyOptions.Member));
        return properties;
    }

    public void SetSession(ISession newSession)
    {
        session = newSession;
        session.Changed += Refresh;
        Refresh();
    }

    private void OnDestroy()
    {
        if (session != null)
        {
            session.Changed -= Refresh;
        }
    }

    private void Refresh()
    {
        string playerList = "Players " + session.Players.Count + "/" + session.MaxPlayers;
        foreach (IReadOnlyPlayer player in session.Players)
        {
            playerList += "\n" + GetDisplayName(player);
        }

        playerListText.text = playerList;
    }

    private string GetDisplayName(IReadOnlyPlayer player)
    {
        PlayerProperty nameProperty;
        if (player.Properties.TryGetValue(DisplayNameKey, out nameProperty))
        {
            return nameProperty.Value;
        }

        return "Unknown";
    }
}
