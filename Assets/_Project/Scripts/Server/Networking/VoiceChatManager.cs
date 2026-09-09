using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Unity.Services.Vivox;
using UnityEngine;
using UnityEngine.InputSystem;

public class VoiceChatManager : MonoBehaviour
{
    public static VoiceChatManager Instance { get; private set; }

    [Header("Proximity tuning")]
    [Tooltip("Distance at which a voice is heard at full volume. Roughly half the player's height.")]
    [SerializeField] private int conversationalDistance = 1;
    [Tooltip("Maximum distance at which a voice can be heard at all.")]
    [SerializeField] private int audibleDistance = 20;

    [Header("Controls")]
    [Tooltip("Press to toggle your mic. Starts unmuted (open mic).")]
    [SerializeField] private Key muteToggleKey = Key.M;

    public string PositionalChannelName { get; private set; } = string.Empty;

    private bool _loggedIn;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public async Task LoginAsync(string displayName)
    {
        if (_loggedIn) return;
        try
        {
            await VivoxService.Instance.InitializeAsync();
            await VivoxService.Instance.LoginAsync(new LoginOptions { DisplayName = displayName });
            _loggedIn = true;
            Debug.Log("Vivox logged in.");
        }
        catch (Exception e)
        {
            Debug.LogError($"Vivox login failed: {e}");
        }
    }

    public async Task JoinSessionVoiceAsync(string sessionId)
    {
        if (!_loggedIn)
        {
            Debug.LogWarning("Vivox not logged in yet; skipping voice join.");
            return;
        }

        string channelName = "s" + Regex.Replace(sessionId, "[^a-zA-Z0-9]", "");
        if (channelName.Length > 40) channelName = channelName.Substring(0, 40);

        try
        {
            var props = new Channel3DProperties(audibleDistance, conversationalDistance, 1.0f, AudioFadeModel.InverseByDistance);
            await VivoxService.Instance.JoinPositionalChannelAsync(channelName, ChatCapability.AudioOnly, props);
            PositionalChannelName = channelName;
            Debug.Log($"Joined positional voice channel: {channelName}");
        }
        catch (Exception e)
        {
            Debug.LogError($"Join voice channel failed: {e}");
        }
    }

    public async Task LeaveSessionVoiceAsync()
    {
        if (string.IsNullOrEmpty(PositionalChannelName)) return;
        try
        {
            await VivoxService.Instance.LeaveChannelAsync(PositionalChannelName);
        }
        catch (Exception e)
        {
            Debug.LogError($"Leave voice channel failed: {e}");
        }
        finally
        {
            PositionalChannelName = string.Empty;
        }
    }

    private void Update()
    {
        if (!_loggedIn || Keyboard.current == null) return;

        if (Keyboard.current[muteToggleKey].wasPressedThisFrame)
        {
            if (VivoxService.Instance.IsInputDeviceMuted)
            {
                VivoxService.Instance.UnmuteInputDevice();
            } else
            {
                VivoxService.Instance.MuteInputDevice();
            }

            Debug.Log($"Mic muted: {VivoxService.Instance.IsInputDeviceMuted}");
        }
    }
}
