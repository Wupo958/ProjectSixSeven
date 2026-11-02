using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public struct LevelStats : INetworkSerializeByMemcpy
{
    public int Kills;
    public int DamageTaken;
    public int Repairs;
    public float Time;
}

public sealed class GameSession : NetworkBehaviour
{
    public static GameSession Instance { get; private set; }

    [SerializeField] private string gameplayScene = "ProcGenTest";

    private readonly NetworkVariable<int> levelIndex = new NetworkVariable<int>(0);
    private readonly NetworkVariable<LevelStats> current = new NetworkVariable<LevelStats>();
    private readonly NetworkVariable<LevelStats> total = new NetworkVariable<LevelStats>();
    private readonly NetworkVariable<bool> lastLevelWon = new NetworkVariable<bool>(false);

    private double levelStartTime;
    private bool levelRunning;

    public event Action StatsChanged;

    public string GameplayScene => gameplayScene;
    public int LevelIndex => levelIndex.Value;
    public LevelStats Current => current.Value;
    public LevelStats Total => total.Value;
    public bool LastLevelWon => lastLevelWon.Value;

    public override void OnNetworkSpawn()
    {
        Instance = this;

        levelIndex.OnValueChanged += OnLevelChanged;
        current.OnValueChanged += OnStatsChanged;
        total.OnValueChanged += OnStatsChanged;
        lastLevelWon.OnValueChanged += OnResultChanged;
    }

    public override void OnNetworkDespawn()
    {
        levelIndex.OnValueChanged -= OnLevelChanged;
        current.OnValueChanged -= OnStatsChanged;
        total.OnValueChanged -= OnStatsChanged;
        lastLevelWon.OnValueChanged -= OnResultChanged;

        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void BeginLevel()
    {
        if (!IsServer)
        {
            return;
        }

        current.Value = new LevelStats();
        levelStartTime = NetworkManager.ServerTime.Time;
        levelRunning = true;
    }

    public void EndLevel(bool won)
    {
        if (!IsServer || !levelRunning)
        {
            return;
        }

        levelRunning = false;
        lastLevelWon.Value = won;

        LevelStats stats = current.Value;
        stats.Time = (float)(NetworkManager.ServerTime.Time - levelStartTime);
        current.Value = stats;

        LevelStats sum = total.Value;
        sum.Kills += stats.Kills;
        sum.DamageTaken += stats.DamageTaken;
        sum.Repairs += stats.Repairs;
        sum.Time += stats.Time;
        total.Value = sum;
    }

    public void AddKill()
    {
        if (!IsServer || !levelRunning)
        {
            return;
        }

        LevelStats stats = current.Value;
        stats.Kills++;
        current.Value = stats;
    }

    public void AddDamage(int amount)
    {
        if (!IsServer || !levelRunning)
        {
            return;
        }

        LevelStats stats = current.Value;
        stats.DamageTaken += amount;
        current.Value = stats;
    }

    public void AddRepair()
    {
        if (!IsServer || !levelRunning)
        {
            return;
        }

        LevelStats stats = current.Value;
        stats.Repairs++;
        current.Value = stats;
    }

    public void LoadNextLevel()
    {
        if (!IsServer)
        {
            return;
        }

        levelIndex.Value++;
        LoadGameplay();
    }

    public void RestartGame()
    {
        if (!IsServer)
        {
            return;
        }

        levelIndex.Value = 0;
        current.Value = new LevelStats();
        total.Value = new LevelStats();
        LoadGameplay();
    }

    public void LoadGameplay()
    {
        if (!IsServer)
        {
            return;
        }

        NetworkManager.SceneManager.LoadScene(gameplayScene, LoadSceneMode.Single);
    }

    private void OnLevelChanged(int previous, int next)
    {
        StatsChanged?.Invoke();
    }

    private void OnStatsChanged(LevelStats previous, LevelStats next)
    {
        StatsChanged?.Invoke();
    }

    private void OnResultChanged(bool previous, bool next)
    {
        StatsChanged?.Invoke();
    }
}
