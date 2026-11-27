using System.Collections.Generic;
using ProjectSixSeven.Shared.Track;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class EnemySpawner : NetworkBehaviour
{
    [SerializeField] private GameObject train;

    [Header("Wave timing")]
    [Tooltip("Seconds before the first wave.")]
    [SerializeField] private float firstWaveDelay = 5f;

    [Tooltip("Random seconds added to or taken from every pause between waves.")]
    [SerializeField] private float waveIntervalJitter = 10f;

    [Header("Spawn position (relative to the train)")]
    [Tooltip("Distance behind the train's tail, measured along the track, in metres (min, max).")]
    [SerializeField] private Vector2 distanceBehindTrain = new Vector2(100f, 250f);

    [Tooltip("Sideways distance from the track centre, in metres (min, max). The side is random.")]
    [SerializeField] private Vector2 sideOffset = new Vector2(20f, 100f);

    [Tooltip("Radius in which the members of one group are scattered around the group centre, in metres.")]
    [SerializeField] private float groupSpread = 15f;

    [Header("Fallback without a route (testing the scene on its own)")]
    [SerializeField] private EnemyBase[] enemyTypes;
    [SerializeField] private int minPoolSize = 1;
    [SerializeField] private int maxPoolSize = 5;
    [SerializeField] private float waveInterval = 45f;
    [SerializeField] private int minGroupsPerWave = 1;
    [SerializeField] private int maxGroupsPerWave = 3;

    private readonly List<EnemyBase> enemyPool = new List<EnemyBase>();
    private Trainbuilder trainBuilder;
    private float nextWaveTime;
    private float currentWaveInterval;
    private int currentMinGroups;
    private int currentMaxGroups;

    public override void OnNetworkSpawn()
    {
        if (!IsServer)
        {
            return;
        }

        if (train != null)
        {
            trainBuilder = train.GetComponent<Trainbuilder>();
        }

        if (trainBuilder == null)
        {
            trainBuilder = Trainbuilder.Instance;
        }

        RouteOption route = default;
        EnemyRegistry registry = null;
        if (GameSession.Instance != null)
        {
            route = GameSession.Instance.CurrentRoute;
            registry = GameSession.Instance.EnemyRegistry;
        }

        if (route.IsValid)
        {
            ApplyRoute(route, registry);
        }
        else
        {
            ApplyFallbackWaves();
        }

        if (enemyPool.Count == 0)
        {
            RollFallbackPool();
        }

        nextWaveTime = Time.time + firstWaveDelay;
    }

    private void Update()
    {
        if (!IsServer || Time.time < nextWaveTime)
        {
            return;
        }

        SpawnWave();
        nextWaveTime = Time.time + NextPause();
    }

    private void ApplyRoute(RouteOption route, EnemyRegistry registry)
    {
        currentWaveInterval = route.WaveInterval;
        currentMinGroups = route.WaveSize;
        currentMaxGroups = route.WaveSize;

        enemyPool.Clear();
        if (registry == null)
        {
            return;
        }

        for (int i = 0; i < route.EnemyPool.Length; i++)
        {
            EnemyBase enemy = registry.Get(route.EnemyPool[i]);
            if (enemy != null)
            {
                enemyPool.Add(enemy);
            }
        }
    }

    private void ApplyFallbackWaves()
    {
        currentWaveInterval = waveInterval;
        currentMinGroups = minGroupsPerWave;
        currentMaxGroups = maxGroupsPerWave;
    }

    private void RollFallbackPool()
    {
        enemyPool.Clear();
        if (enemyTypes == null || enemyTypes.Length == 0)
        {
            return;
        }

        int poolSize = Random.Range(minPoolSize, maxPoolSize + 1);
        FixedList32Bytes<int> pool = RouteGenerator.RollEnemyPool(new System.Random(), poolSize, enemyTypes.Length);

        for (int i = 0; i < pool.Length; i++)
        {
            enemyPool.Add(enemyTypes[pool[i]]);
        }
    }

    private float NextPause()
    {
        return Mathf.Max(1f, currentWaveInterval + Random.Range(-waveIntervalJitter, waveIntervalJitter));
    }

    private void SpawnWave()
    {
        if (enemyPool.Count == 0)
        {
            return;
        }

        int groupCount = Random.Range(currentMinGroups, currentMaxGroups + 1);
        for (int i = 0; i < groupCount; i++)
        {
            EnemyBase enemyType = enemyPool[Random.Range(0, enemyPool.Count)];
            SpawnGroup(enemyType);
        }
    }

    private void SpawnGroup(EnemyBase prefab)
    {
        float side = 1f;
        if (Random.value < 0.5f)
        {
            side = -1f;
        }

        GetSpawnFrame(out Vector3 trackPoint, out Vector3 forward);
        Vector3 right = Vector3.Cross(Vector3.up, forward);

        Vector3 groupCenter = trackPoint + right * (side * Random.Range(sideOffset.x, sideOffset.y));
        groupCenter.y = prefab.SpawnHeight;

        Quaternion facing = Quaternion.LookRotation(forward, Vector3.up);

        int groupSize = Random.Range(prefab.MinGroupSize, prefab.MaxGroupSize + 1);
        for (int i = 0; i < groupSize; i++)
        {
            Vector2 scatter = Random.insideUnitCircle * groupSpread;
            Vector3 spawnPoint = groupCenter + new Vector3(scatter.x, 0f, scatter.y);
            EnemyBase enemy = Instantiate(prefab, spawnPoint, facing);
            enemy.SetFormation(i, side, groupCenter);
            enemy.NetworkObject.Spawn(true);
        }
    }

    private void GetSpawnFrame(out Vector3 point, out Vector3 forward)
    {
        float behind = Random.Range(distanceBehindTrain.x, distanceBehindTrain.y);

        TrackPath path = null;
        if (trainBuilder != null)
        {
            path = trainBuilder.Path;
        }

        if (path == null || !path.IsValid)
        {
            GetFallbackSpawnFrame(behind, out point, out forward);
            return;
        }

        float along = trainBuilder.NoseDistance - trainBuilder.TrainLength - behind;

        if (trainBuilder.IsLoopTrack)
        {
            along = Mathf.Repeat(along, path.Length);
        }

        TrackSample sample = path.Sample(Mathf.Clamp(along, 0f, path.Length));
        forward = Flatten(sample.Forward);
        point = sample.Position;

        if (along < 0f)
        {
            point += forward * along;
        }
    }

    private void GetFallbackSpawnFrame(float behind, out Vector3 point, out Vector3 forward)
    {
        Transform origin = transform;
        if (train != null)
        {
            origin = train.transform;
        }

        forward = Flatten(origin.forward);
        point = origin.position - forward * behind;
    }

    private static Vector3 Flatten(Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
        {
            return Vector3.forward;
        }

        return direction.normalized;
    }

    // ----- debug menu ----------------------------------------------------------------

    public void DebugSpawnGroup(EnemyBase prefab)
    {
        if (IsServer && prefab != null) SpawnGroup(prefab);
    }

    public void DebugSpawnWave()
    {
        if (IsServer) SpawnWave();
    }
}
