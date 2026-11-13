using Unity.Netcode;
using UnityEngine;

public static class MedalAwards
{
    public static void Award(MedalDefinition medal, ulong clientId)
    {
        if (!CanAward(medal))
        {
            return;
        }

        GameSession.Instance.AwardMedal(clientId, medal.Id);
    }

    public static void Award(MedalDefinition medal, GameObject player)
    {
        ulong clientId = player.GetComponentInParent<NetworkObject>().OwnerClientId;
        Award(medal, clientId);
    }

    public static void AwardAll(MedalDefinition medal)
    {
        if (!CanAward(medal))
        {
            return;
        }

        GameSession.Instance.AwardMedalToAll(medal.Id);
    }

    private static bool CanAward(MedalDefinition medal)
    {
        if (!NetworkManager.Singleton.IsServer)
        {
            Debug.LogWarning("Medal " + medal.Id + " can only be awarded on the server.");
            return false;
        }

        return true;
    }
}
