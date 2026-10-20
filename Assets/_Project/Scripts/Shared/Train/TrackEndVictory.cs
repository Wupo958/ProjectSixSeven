using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(NetworkObject))]
public class TrackEndVictory : NetworkBehaviour
{
    [SerializeField] private Trainbuilder trainBuilder;
    [SerializeField] private float winDistanceFromEnd = 1f;
    [SerializeField] private string winScene = "GameWonScreen";

    private bool hasWon;

    private void Awake()
    {
        if (trainBuilder == null)
        {
            trainBuilder = GetComponent<Trainbuilder>();
        }
    }

    private void Update()
    {
        if (!IsServer || hasWon || trainBuilder == null)
        {
            return;
        }

        if (trainBuilder.IsLoopTrack)
        {
            return;
        }

        float length = trainBuilder.TrackLength;
        if (length < 0.01f)
        {
            return;
        }

        if (trainBuilder.NoseDistance < length - winDistanceFromEnd)
        {
            return;
        }

        if (TrainHealth.Instance != null && TrainHealth.Instance.IsDead)
        {
            return;
        }

        TriggerVictory();
    }

    private void TriggerVictory()
    {
        hasWon = true;
        Debug.Log("REACHED END OF TRACK - VICTORY");

        if (PlayerLifecycle.Main != null)
        {
            PlayerLifecycle.Main.DespawnAllPlayers();
        }

        NetworkManager.Singleton.SceneManager.LoadScene(winScene, LoadSceneMode.Single);
    }
}
