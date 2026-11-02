using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Unity.Services.Vivox;
using UnityEngine;
using UnityEngine.InputSystem;

public class VoiceChatManager : MonoBehaviour
{
    public static VoiceChatManager Instance { get; private set; }

    [SerializeField] private int conversationalDistance = 1;
    [SerializeField] private int audibleDistance = 20;

    [SerializeField] private Key muteToggleKey = Key.M;

    public string PositionalChannelName { get; private set; } = string.Empty;

    private string sessionId = string.Empty;
    private string activeChannelName = string.Empty;
    private bool isLoggedIn;

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
        if (isLoggedIn)
        {
            return;
        }

        try
        {
            await VivoxService.Instance.InitializeAsync();
            await VivoxService.Instance.LoginAsync(new LoginOptions { DisplayName = displayName });
            isLoggedIn = true;
            Debug.Log("Vivox logged in.");
        }
        catch (Exception e)
        {
            Debug.LogError("Vivox login failed: " + e);
        }
    }

    public void SetSession(string id)
    {
        sessionId = id;
    }

    public async Task SwitchToLobbyAsync()
    {
        await LeaveActiveChannelAsync();

        if (!CanJoin())
        {
            return;
        }

        string channelName = BuildChannelName("l");

        try
        {
            await VivoxService.Instance.JoinGroupChannelAsync(channelName, ChatCapability.AudioOnly);
            activeChannelName = channelName;
            Debug.Log("Joined lobby voice channel: " + channelName);
        }
        catch (Exception e)
        {
            Debug.LogError("Join lobby voice channel failed: " + e);
        }
    }

    public async Task SwitchToPositionalAsync()
    {
        await LeaveActiveChannelAsync();

        if (!CanJoin())
        {
            return;
        }

        string channelName = BuildChannelName("s");

        try
        {
            Channel3DProperties properties = new Channel3DProperties(
                audibleDistance, conversationalDistance, 1.0f, AudioFadeModel.InverseByDistance);

            await VivoxService.Instance.JoinPositionalChannelAsync(channelName, ChatCapability.AudioOnly, properties);
            activeChannelName = channelName;
            PositionalChannelName = channelName;
            Debug.Log("Joined positional voice channel: " + channelName);
        }
        catch (Exception e)
        {
            Debug.LogError("Join positional voice channel failed: " + e);
        }
    }

    public async Task LeaveActiveChannelAsync()
    {
        PositionalChannelName = string.Empty;

        if (string.IsNullOrEmpty(activeChannelName))
        {
            return;
        }

        string channelName = activeChannelName;
        activeChannelName = string.Empty;

        try
        {
            await VivoxService.Instance.LeaveChannelAsync(channelName);
        }
        catch (Exception e)
        {
            Debug.LogError("Leave voice channel failed: " + e);
        }
    }

    private bool CanJoin()
    {
        if (!isLoggedIn)
        {
            Debug.LogWarning("Vivox not logged in yet; skipping voice join.");
            return false;
        }

        return !string.IsNullOrEmpty(sessionId);
    }

    private string BuildChannelName(string prefix)
    {
        string channelName = prefix + Regex.Replace(sessionId, "[^a-zA-Z0-9]", "");
        if (channelName.Length > 40)
        {
            channelName = channelName.Substring(0, 40);
        }

        return channelName;
    }

    private void Update()
    {
        if (!isLoggedIn || Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current[muteToggleKey].wasPressedThisFrame)
        {
            if (VivoxService.Instance.IsInputDeviceMuted)
            {
                VivoxService.Instance.UnmuteInputDevice();
            }
            else
            {
                VivoxService.Instance.MuteInputDevice();
            }

            Debug.Log("Mic muted: " + VivoxService.Instance.IsInputDeviceMuted);
        }
    }
}
