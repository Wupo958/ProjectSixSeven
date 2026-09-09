using Unity.Netcode;
using UnityEngine;
using ProjectSixSeven.Shared.Track;

public sealed class NetworkTrainClock : MonoBehaviour {
    private void OnEnable() {
        TrainFollower.TimeSource = GetNetworkSeconds;
    }

    private void OnDisable() {
        if (TrainFollower.TimeSource == GetNetworkSeconds) {
            TrainFollower.TimeSource = null;
        }
    }

    private static float GetNetworkSeconds() {
        NetworkManager nm = NetworkManager.Singleton;

        if (nm != null && nm.IsListening) {
            return (float)nm.ServerTime.Time;
        }

        return Time.timeSinceLevelLoad;
    }
}