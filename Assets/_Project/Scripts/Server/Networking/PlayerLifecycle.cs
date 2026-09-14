using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerLifecycle : MonoBehaviour
{
    public static PlayerLifecycle Main { get; private set; }

    [SerializeField] private string menuScene = "MultiplayerTest";

    private bool isSubscribedToLoadEvents;

    private void Awake()
    {
        if (Main != null)
        {
            Destroy(gameObject);
            return;
        }

        Main = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        NetworkManager.Singleton.OnServerStarted += SubscribeToLoadEvents;
        NetworkManager.Singleton.OnClientStopped += OnClientStopped;
    }

    private void OnDestroy()
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager != null)
        {
            networkManager.OnServerStarted -= SubscribeToLoadEvents;
            networkManager.OnClientStopped -= OnClientStopped;
            if (isSubscribedToLoadEvents)
            {
                networkManager.SceneManager.OnLoadEventCompleted -= OnSceneLoadCompleted;
                isSubscribedToLoadEvents = false;
            }
        }

        if (Main == this)
        {
            Main = null;
        }
    }

    public void DespawnAllPlayers()
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        if (!networkManager.IsServer)
        {
            return;
        }

        List<NetworkObject> playerObjects = new List<NetworkObject>();
        foreach (NetworkClient client in networkManager.ConnectedClientsList)
        {
            if (client.PlayerObject != null)
            {
                playerObjects.Add(client.PlayerObject);
            }
        }

        for (int i = 0; i < playerObjects.Count; i++)
        {
            playerObjects[i].Despawn(true);
        }
    }

    public void SpawnMissingPlayers()
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        if (!networkManager.IsServer)
        {
            return;
        }

        NetworkObject playerPrefab = networkManager.NetworkConfig.PlayerPrefab.GetComponent<NetworkObject>();

        List<ulong> clientIds = new List<ulong>();
        foreach (NetworkClient client in networkManager.ConnectedClientsList)
        {
            if (client.PlayerObject == null)
            {
                clientIds.Add(client.ClientId);
            }
        }

        for (int i = 0; i < clientIds.Count; i++)
        {
            networkManager.SpawnManager.InstantiateAndSpawn(playerPrefab, clientIds[i], false, true);
        }
    }

    private void OnClientStopped(bool wasHost)
    {
        if (VoiceChatManager.Instance != null)
        {
            _ = VoiceChatManager.Instance.LeaveActiveChannelAsync();
        }

        SceneManager.LoadScene(menuScene);
    }

    private void SubscribeToLoadEvents()
    {
        NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += OnSceneLoadCompleted;
        isSubscribedToLoadEvents = true;
    }

    private void OnSceneLoadCompleted(string sceneName, LoadSceneMode loadSceneMode,
        List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
    {
        bool isGameplayScene = TrainHealth.Instance != null;

        if (VoiceChatManager.Instance != null)
        {
            if (isGameplayScene)
            {
                _ = VoiceChatManager.Instance.SwitchToPositionalAsync();
            }
            else
            {
                _ = VoiceChatManager.Instance.SwitchToLobbyAsync();
            }
        }

        if (isGameplayScene)
        {
            SpawnMissingPlayers();
        }
    }
}
