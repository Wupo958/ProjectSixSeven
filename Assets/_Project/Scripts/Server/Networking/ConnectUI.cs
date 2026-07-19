using System;
using System.Threading.Tasks;
using TMPro;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using UnityEngine.SceneManagement;

public class ConnectUI : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField] private Button hostButton;
    [SerializeField] private Button joinButton;

    [Header("Join code")]
    [SerializeField] private TMP_InputField joinCodeInput;
    [SerializeField] private TMP_Text joinCodeDisplay;

    [Header("Session")]
    [SerializeField] private int maxPlayers = 4;

    [Tooltip("This is optional.")]
    [SerializeField] private GameObject panelToHideOnConnect;

    private ISession session;

    private async void Start()
    {
        SetButtonsInteractable(false);

        try
        {
            await UnityServices.InitializeAsync();

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }

            if (VoiceChatManager.Instance != null)
            {
                await VoiceChatManager.Instance.LoginAsync($"Player-{UnityEngine.Random.Range(1000, 9999)}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Unity Services init/sign-in failed: {e}");
            return;
        }

        hostButton.onClick.AddListener(() => _ = CreateSession());
        joinButton.onClick.AddListener(() => _ = JoinSession());
        SetButtonsInteractable(true);
    }

    private async Task CreateSession()
    {
        SetButtonsInteractable(false);
        try
        {
            var options = new SessionOptions { MaxPlayers = maxPlayers }.WithRelayNetwork();
            session = await MultiplayerService.Instance.CreateSessionAsync(options);

            if (joinCodeDisplay != null)
            {
                joinCodeDisplay.text = $"Code: {session.Code}";
            }

            if (NetworkManager.Singleton.IsServer)
            {
                NetworkManager.Singleton.SceneManager.LoadScene("RailTest", LoadSceneMode.Single);
            }

            Debug.Log($"Session created. Join code: {session.Code}");
            OnConnected();
        }
        catch (Exception e)
        {
            Debug.LogError($"Create session failed: {e}");
            SetButtonsInteractable(true);
        }
    }

    private async Task JoinSession()
    {
        string code = joinCodeInput != null ? joinCodeInput.text.Trim() : string.Empty;
        if (string.IsNullOrEmpty(code))
        {
            Debug.LogWarning("Enter a join code first.");
            return;
        }

        SetButtonsInteractable(false);
        try
        {
            session = await MultiplayerService.Instance.JoinSessionByCodeAsync(code);
            Debug.Log($"Joined session {session.Id}");
            OnConnected();
        }
        catch (Exception e)
        {
            Debug.LogError($"Join session failed: {e}");
            SetButtonsInteractable(true);
        }
    }

    private async Task JoinVoice()
    {
        if (VoiceChatManager.Instance != null && session != null)
        {
            await VoiceChatManager.Instance.JoinSessionVoiceAsync(session.Id);
        }
    }

    private void OnConnected()
    {
        if (panelToHideOnConnect != null)
        {
            panelToHideOnConnect.SetActive(false);
        } 
    }

    private void SetButtonsInteractable(bool value)
    {
        if (hostButton != null) hostButton.interactable = value;
        if (joinButton != null) joinButton.interactable = value;
    }
}