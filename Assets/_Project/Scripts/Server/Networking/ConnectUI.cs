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

    [Tooltip("Optional. If left empty, the Host button is reused as the Start button after hosting.")]
    [SerializeField] private Button startButton;

    [Header("Join code")]
    [SerializeField] private TMP_InputField joinCodeInput;
    [SerializeField] private TMP_Text joinCodeDisplay;

    [Header("Session")]
    [SerializeField] private int maxPlayers = 4;

    [Tooltip("Gameplay scene the host network-loads on Start. Must be in Build Settings.")]
    [SerializeField] private string gameplayScene = "RailTestMultiplayer";

    [Tooltip("Optional. Hidden once the game starts (the lobby scene unloads anyway on start).")]
    [SerializeField] private GameObject panelToHideOnConnect;

    private ISession session;
    private bool subscribedToClients;

    private async void Start()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        SetButtonsInteractable(false);

        if (startButton != null)
        {
            startButton.gameObject.SetActive(false);
        }

        try
        {
            await UnityServices.InitializeAsync();

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                try
                {
                    string profile = "p" + Guid.NewGuid().ToString("N").Substring(0, 20);
                    AuthenticationService.Instance.SwitchProfile(profile);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Could not switch auth profile: {e.Message}");
                }

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

            Debug.Log($"Session created. Join code: {session.Code}");
            EnterHostLobby();
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
            EnterClientLobby();
        }
        catch (Exception e)
        {
            Debug.LogError($"Join session failed: {e}");
            SetButtonsInteractable(true);
        }
    }

    private void EnterHostLobby()
    {
        if (joinButton != null) joinButton.gameObject.SetActive(false);
        if (joinCodeInput != null) joinCodeInput.gameObject.SetActive(false);

        SubscribeToClientChanges();
        UpdateHostLobbyText();

        if (startButton != null)
        {
            startButton.gameObject.SetActive(true);
            startButton.onClick.RemoveAllListeners();
            startButton.onClick.AddListener(StartGame);
            startButton.interactable = true;
            if (hostButton != null) hostButton.gameObject.SetActive(false);
        }
        else if (hostButton != null)
        {
            hostButton.onClick.RemoveAllListeners();
            hostButton.onClick.AddListener(StartGame);
            hostButton.interactable = true;

            TMP_Text label = hostButton.GetComponentInChildren<TMP_Text>();
            if (label != null) label.text = "Start Game";
        }
    }

    private async void EnterClientLobby()
    {
        if (hostButton != null) hostButton.gameObject.SetActive(false);
        if (joinButton != null) joinButton.gameObject.SetActive(false);
        if (joinCodeInput != null) joinCodeInput.gameObject.SetActive(false);
        if (startButton != null) startButton.gameObject.SetActive(false);

        if (VoiceChatManager.Instance != null && session != null) {
            await VoiceChatManager.Instance.JoinSessionVoiceAsync(session.Id);
        }

        if (joinCodeDisplay != null)
        {
            joinCodeDisplay.text = "Joined! Waiting for the host to start…";
        }
    }

    private async void StartGame()
    {
        NetworkManager nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsServer)
        {
            Debug.LogWarning("Only the host can start the game.");
            return;
        }

        if (panelToHideOnConnect != null)
        {
            panelToHideOnConnect.SetActive(false);
        }

        if (VoiceChatManager.Instance != null && session != null) {
            await VoiceChatManager.Instance.JoinSessionVoiceAsync(session.Id);
        }

        nm.SceneManager.LoadScene(gameplayScene, LoadSceneMode.Single);
    }

    private void SubscribeToClientChanges()
    {
        NetworkManager nm = NetworkManager.Singleton;
        if (nm == null || subscribedToClients)
        {
            return;
        }

        nm.OnClientConnectedCallback += OnClientChanged;
        nm.OnClientDisconnectCallback += OnClientChanged;
        subscribedToClients = true;
    }

    private void OnClientChanged(ulong clientId)
    {
        UpdateHostLobbyText();
    }

    private void UpdateHostLobbyText()
    {
        if (joinCodeDisplay == null || session == null)
        {
            return;
        }

        NetworkManager nm = NetworkManager.Singleton;
        int players = nm != null && nm.IsServer ? nm.ConnectedClients.Count : 1;
        joinCodeDisplay.text = $"Code: {session.Code}\nPlayers: {players}/{maxPlayers}";
    }

    private void OnDestroy()
    {
        NetworkManager nm = NetworkManager.Singleton;
        if (nm != null && subscribedToClients)
        {
            nm.OnClientConnectedCallback -= OnClientChanged;
            nm.OnClientDisconnectCallback -= OnClientChanged;
        }
    }

    private void SetButtonsInteractable(bool value)
    {
        if (hostButton != null) hostButton.interactable = value;
        if (joinButton != null) joinButton.interactable = value;
    }
}
