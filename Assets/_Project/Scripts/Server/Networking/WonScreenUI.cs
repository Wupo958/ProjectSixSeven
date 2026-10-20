using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class WonScreenUI : NetworkBehaviour
{
    [SerializeField] private Button restartButton;
    [SerializeField] private string gameplayScene = "ProcGenTest";

    public override void OnNetworkSpawn()
    {
        restartButton.interactable = IsServer;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void RestartGame()
    {
        if (!IsServer)
        {
            return;
        }

        NetworkManager.Singleton.SceneManager.LoadScene(gameplayScene, LoadSceneMode.Single);
    }

    public void ReturnLobby()
    {
        NetworkManager.Singleton.Shutdown();
    }
}
