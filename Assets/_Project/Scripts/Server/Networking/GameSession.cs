using System;
using Unity.Collections;
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
    public const int NoMajority = -1;

    private const ulong NoClient = ulong.MaxValue;

    public static GameSession Instance { get; private set; }

    [SerializeField] private string gameplayScene = "ProcGenTest";

    [SerializeField] private MedalDefinition startGameMedal;
    [SerializeField] private MedalDefinition firstKillMedal;

    [Header("Routes")]
    [SerializeField] private RouteGenerator routeGenerator = new RouteGenerator();

    [Tooltip("Enemy types a route's enemy pool is rolled from.")]
    [SerializeField] private EnemyRegistry enemyRegistry;

    [Tooltip("How many routes are offered at a station.")]
    [SerializeField, Range(1, 3)] private int routeOptionCount = 3;

    [Header("Resources")]
    [Tooltip("Team resources at the start of a run.")]
    [SerializeField, Min(0)] private int startingResources = 0;

    private readonly NetworkVariable<int> levelIndex = new NetworkVariable<int>(0);
    private readonly NetworkVariable<int> teamResources = new NetworkVariable<int>(0);
    private readonly NetworkVariable<bool> runStarted = new NetworkVariable<bool>(false);
    private readonly NetworkVariable<LevelStats> current = new NetworkVariable<LevelStats>();
    private readonly NetworkVariable<LevelStats> total = new NetworkVariable<LevelStats>();
    private readonly NetworkVariable<bool> lastLevelWon = new NetworkVariable<bool>(false);

    private readonly NetworkVariable<RouteOption> currentRoute = new NetworkVariable<RouteOption>();
    private readonly NetworkList<RouteOption> routeOptions = new NetworkList<RouteOption>();
    private readonly NetworkList<RouteVote> routeVotes = new NetworkList<RouteVote>();
    private readonly NetworkVariable<int> majorityOption = new NetworkVariable<int>(NoMajority);
    private readonly NetworkVariable<int> requiredVotes = new NetworkVariable<int>(1);

    private readonly System.Random routeRng = new System.Random();

    private double levelStartTime;
    private bool levelRunning;

    public event Action StatsChanged;
    public event Action RoutesChanged;
    public event Action ResourcesChanged;

    public string GameplayScene => gameplayScene;
    public int LevelIndex => levelIndex.Value;
    public LevelStats Current => current.Value;
    public LevelStats Total => total.Value;
    public bool LastLevelWon => lastLevelWon.Value;
    public int TeamResources => teamResources.Value;
    public bool RunStarted => runStarted.Value;

    public RouteOption CurrentRoute => currentRoute.Value;
    public int RouteOptionCount => routeOptions.Count;
    public EnemyRegistry EnemyRegistry => enemyRegistry;
    public int VoteCount => routeVotes.Count;
    public int MajorityOption => majorityOption.Value;
    public int RequiredVotes => requiredVotes.Value;

    private int EnemyTypeCount
    {
        get
        {
            if (enemyRegistry == null)
            {
                return 0;
            }

            return enemyRegistry.Count;
        }
    }

    public RouteOption GetRouteOption(int index)
    {
        return routeOptions[index];
    }

    public RouteVote GetVote(int index)
    {
        return routeVotes[index];
    }

    public override void OnNetworkSpawn()
    {
        Instance = this;

        levelIndex.OnValueChanged += OnLevelChanged;
        current.OnValueChanged += OnStatsChanged;
        total.OnValueChanged += OnStatsChanged;
        lastLevelWon.OnValueChanged += OnResultChanged;
        currentRoute.OnValueChanged += OnRouteChanged;
        routeOptions.OnListChanged += OnRouteOptionsChanged;
        routeVotes.OnListChanged += OnRouteVotesChanged;
        majorityOption.OnValueChanged += OnVoteStateChanged;
        requiredVotes.OnValueChanged += OnVoteStateChanged;
        teamResources.OnValueChanged += OnResourcesChanged;
        runStarted.OnValueChanged += OnRunStartedChanged;

        if (!IsServer)
        {
            return;
        }

        NetworkManager.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;

        if (!currentRoute.Value.IsValid)
        {
            ResetRoutes();
            teamResources.Value = startingResources;
        }

        RecalculateMajority(NoClient);
    }

    public override void OnNetworkDespawn()
    {
        levelIndex.OnValueChanged -= OnLevelChanged;
        current.OnValueChanged -= OnStatsChanged;
        total.OnValueChanged -= OnStatsChanged;
        lastLevelWon.OnValueChanged -= OnResultChanged;
        currentRoute.OnValueChanged -= OnRouteChanged;
        routeOptions.OnListChanged -= OnRouteOptionsChanged;
        routeVotes.OnListChanged -= OnRouteVotesChanged;
        majorityOption.OnValueChanged -= OnVoteStateChanged;
        requiredVotes.OnValueChanged -= OnVoteStateChanged;
        teamResources.OnValueChanged -= OnResourcesChanged;
        runStarted.OnValueChanged -= OnRunStartedChanged;

        if (IsServer && NetworkManager != null)
        {
            NetworkManager.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
        }

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
        runStarted.Value = true;

        MedalAwards.AwardAll(startGameMedal);
    }

    public bool CanAfford(int cost)
    {
        if (!runStarted.Value)
        {
            return true;
        }

        return teamResources.Value >= cost;
    }

    public bool TrySpend(int cost)
    {
        if (!IsServer || !CanAfford(cost))
        {
            return false;
        }

        if (runStarted.Value)
        {
            teamResources.Value -= cost;
        }

        return true;
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

        if (won)
        {
            teamResources.Value += currentRoute.Value.Payout;
            GenerateRouteOptions();
            MissionWonRpc();
        }
    }

    public void CastVote(int optionIndex)
    {
        string playerName = "Player";
        if (ProfileService.Current != null && !string.IsNullOrWhiteSpace(ProfileService.Current.DisplayName))
        {
            playerName = ProfileService.Current.DisplayName;
        }

        CastVoteRpc(optionIndex, playerName);
    }

    public void RequestStart()
    {
        RequestStartRpc();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void CastVoteRpc(int optionIndex, string playerName, RpcParams rpcParams = default)
    {
        if (optionIndex < 0 || optionIndex >= routeOptions.Count)
        {
            return;
        }

        ulong sender = rpcParams.Receive.SenderClientId;

        FixedString32Bytes safeName = default;
        if (playerName != null)
        {
            safeName.CopyFromTruncated(playerName);
        }

        RouteVote vote = new RouteVote
        {
            ClientId = sender,
            Option = optionIndex,
            PlayerName = safeName
        };

        int existing = FindVote(sender);
        if (existing >= 0)
        {
            routeVotes[existing] = vote;
        }
        else
        {
            routeVotes.Add(vote);
        }

        RecalculateMajority(NoClient);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestStartRpc()
    {
        if (majorityOption.Value == NoMajority)
        {
            return;
        }

        StartRoute(majorityOption.Value);
    }

    public void AwardMedal(ulong clientId, string medalId)
    {
        if (!IsServer)
        {
            return;
        }

        UnlockMedalRpc(medalId, RpcTarget.Single(clientId, RpcTargetUse.Temp));
    }

    public void AwardMedalToAll(string medalId)
    {
        if (!IsServer)
        {
            return;
        }

        UnlockMedalForAllRpc(medalId);
    }

    [Rpc(SendTo.Everyone)]
    private void UnlockMedalForAllRpc(string medalId)
    {
        _ = ProfileService.UnlockMedal(medalId);
    }

    [Rpc(SendTo.Everyone)]
    private void MissionWonRpc()
    {
        _ = ProfileService.AddSuccessfulMission();
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void UnlockMedalRpc(string medalId, RpcParams rpcParams)
    {
        _ = ProfileService.UnlockMedal(medalId);
    }

    public void AddKill(ulong killerClientId)
    {
        if (!IsServer || !levelRunning)
        {
            return;
        }

        LevelStats stats = current.Value;
        stats.Kills++;
        current.Value = stats;

        bool wasKilledByPlayer = killerClientId != WeaponBase.NoOccupant;
        if (wasKilledByPlayer)
        {
            MedalAwards.Award(firstKillMedal, killerClientId);
        }
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

        int option = majorityOption.Value;
        if (option == NoMajority)
        {
            option = routeOptions.Count / 2;
        }

        StartRoute(option);
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
        ResetRoutes();
        teamResources.Value = startingResources;
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

    private void StartRoute(int optionIndex)
    {
        if (optionIndex >= 0 && optionIndex < routeOptions.Count)
        {
            currentRoute.Value = routeOptions[optionIndex];
        }

        ClearRouteOptions();

        levelIndex.Value++;
        LoadGameplay();
    }

    private void ResetRoutes()
    {
        ClearRouteOptions();
        currentRoute.Value = routeGenerator.CreateStart(routeRng, EnemyTypeCount);
    }

    private void GenerateRouteOptions()
    {
        ClearRouteOptions();

        foreach (RouteOption option in routeGenerator.CreateOptions(routeOptionCount, routeRng, EnemyTypeCount))
        {
            routeOptions.Add(option);
        }
    }

    private void ClearRouteOptions()
    {
        routeOptions.Clear();
        routeVotes.Clear();
        RecalculateMajority(NoClient);
    }

    private int FindVote(ulong clientId)
    {
        for (int i = 0; i < routeVotes.Count; i++)
        {
            if (routeVotes[i].ClientId == clientId)
            {
                return i;
            }
        }

        return -1;
    }

    private void RecalculateMajority(ulong leavingClientId)
    {
        int players = 0;
        foreach (ulong clientId in NetworkManager.ConnectedClientsIds)
        {
            if (clientId != leavingClientId)
            {
                players++;
            }
        }

        int required = Mathf.Max(1, players / 2 + 1);
        requiredVotes.Value = required;

        int majority = NoMajority;
        for (int option = 0; option < routeOptions.Count; option++)
        {
            int votes = 0;
            for (int i = 0; i < routeVotes.Count; i++)
            {
                if (routeVotes[i].Option == option && routeVotes[i].ClientId != leavingClientId)
                {
                    votes++;
                }
            }

            if (votes >= required)
            {
                majority = option;
                break;
            }
        }

        majorityOption.Value = majority;
    }

    private void OnClientConnected(ulong clientId)
    {
        RecalculateMajority(NoClient);
    }

    private void OnClientDisconnected(ulong clientId)
    {
        for (int i = routeVotes.Count - 1; i >= 0; i--)
        {
            if (routeVotes[i].ClientId == clientId)
            {
                routeVotes.RemoveAt(i);
            }
        }

        RecalculateMajority(clientId);
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

    private void OnRouteChanged(RouteOption previous, RouteOption next)
    {
        RoutesChanged?.Invoke();
    }

    private void OnRouteOptionsChanged(NetworkListEvent<RouteOption> change)
    {
        RoutesChanged?.Invoke();
    }

    private void OnRouteVotesChanged(NetworkListEvent<RouteVote> change)
    {
        RoutesChanged?.Invoke();
    }

    private void OnVoteStateChanged(int previous, int next)
    {
        RoutesChanged?.Invoke();
    }

    private void OnResourcesChanged(int previous, int next)
    {
        StatsChanged?.Invoke();
        ResourcesChanged?.Invoke();
    }

    private void OnRunStartedChanged(bool previous, bool next)
    {
        ResourcesChanged?.Invoke();
    }
}
