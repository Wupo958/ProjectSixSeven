using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class LevelEndScreenUI : NetworkBehaviour
{
    [SerializeField] private TMP_Text resultText;
    [SerializeField] private Button continueButton;
    [SerializeField] private TMP_Text continueLabel;
    [SerializeField] private TMP_Text waitingText;

    [SerializeField] private string wonResult = "You Win";
    [SerializeField] private string lostResult = "Game Over";
    [SerializeField] private string wonContinue = "Next Level";
    [SerializeField] private string lostContinue = "Try Again";
    [SerializeField] private Color wonColor = new Color(0.553f, 0.698f, 0.333f);
    [SerializeField] private Color lostColor = new Color(0.753f, 0.224f, 0.169f);

    private GameSession session;

    public override void OnNetworkSpawn()
    {
        continueButton.interactable = IsServer;

        if (waitingText != null)
        {
            waitingText.gameObject.SetActive(!IsServer);
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        session = GameSession.Instance;
        if (session != null)
        {
            session.StatsChanged += Refresh;
        }

        Refresh();
    }

    public override void OnNetworkDespawn()
    {
        if (session != null)
        {
            session.StatsChanged -= Refresh;
        }
    }

    public void Continue()
    {
        if (!IsServer || session == null)
        {
            return;
        }

        if (session.LastLevelWon)
        {
            session.LoadNextLevel();
        }
        else
        {
            session.RestartGame();
        }
    }

    public void ReturnLobby()
    {
        NetworkManager.Singleton.Shutdown();
    }

    private void Refresh()
    {
        if (session == null)
        {
            return;
        }

        bool won = session.LastLevelWon;

        if (resultText != null)
        {
            resultText.text = won ? wonResult : lostResult;
            resultText.color = won ? wonColor : lostColor;
        }

        if (continueLabel != null)
        {
            continueLabel.text = won ? wonContinue : lostContinue;
        }
    }
}
