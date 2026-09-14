using Unity.Netcode;
using UnityEngine;
using ProjectSixSeven.Shared.Track;

public class NetworkTrainClock : NetworkBehaviour
{
    private readonly NetworkVariable<double> levelStartTime =
        new NetworkVariable<double>(0d,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            levelStartTime.Value = NetworkManager.ServerTime.Time;
        }

        TrainFollower.TimeSource = GetLevelSeconds;
    }

    public override void OnNetworkDespawn()
    {
        if (TrainFollower.TimeSource == GetLevelSeconds)
        {
            TrainFollower.TimeSource = null;
        }
    }

    private float GetLevelSeconds()
    {
        NetworkManager networkManager = NetworkManager.Singleton;

        if (networkManager != null && networkManager.IsListening)
        {
            return (float)(networkManager.ServerTime.Time - levelStartTime.Value);
        }

        return Time.timeSinceLevelLoad;
    }
}
