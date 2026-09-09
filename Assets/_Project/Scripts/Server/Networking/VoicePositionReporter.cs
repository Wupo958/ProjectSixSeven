using Unity.Netcode;
using Unity.Services.Vivox;
using UnityEngine;

public class VoicePositionReporter : NetworkBehaviour
{
    [Tooltip("Vivox recommendation is 2-4 times/sec.")]
    [SerializeField] private float updateInterval = 0.25f;

    private float _nextUpdate;

    void Update()
    {
        if (!IsOwner) return;

        if (Time.time < _nextUpdate) return;
        _nextUpdate = Time.time + updateInterval;

        string channel = VoiceChatManager.Instance != null ? VoiceChatManager.Instance.PositionalChannelName : string.Empty;

        if (string.IsNullOrEmpty(channel)) return;

        VivoxService.Instance.Set3DPosition(gameObject, channel);
    }
}
