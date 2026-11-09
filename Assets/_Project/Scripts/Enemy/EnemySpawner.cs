using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class EnemySpawner : NetworkBehaviour
{
    [SerializeField] private EnemyBase[] enemyTypes;
    [SerializeField] private GameObject train;

    [SerializeField] private int minPoolSize = 1;
    [SerializeField] private int maxPoolSize = 5;

    [SerializeField] private float waveInterval = 45f;
    [SerializeField] private float firstWaveDelay = 5f;
    [SerializeField] private int minGroupsPerWave = 1;
    [SerializeField] private int maxGroupsPerWave = 3;
    [SerializeField] private float groupSpread = 15f;

    private List<EnemyBase> enemyPool = new List<EnemyBase>();
    private float nextWaveTime;

    public override void OnNetworkSpawn()
    {
        if (!IsServer)
        {
            return;
        }

        RollEnemyPool();
        nextWaveTime = Time.time + firstWaveDelay;
    }

    private void Update()
    {
        if (!IsServer)
        {
            return;
        }

        if (Time.time < nextWaveTime)
        {
            return;
        }

        SpawnWave();
        nextWaveTime = Time.time + waveInterval;
    }

    private void RollEnemyPool()
    {
        int poolSize = Random.Range(minPoolSize, maxPoolSize + 1);

        do
        {
            enemyPool.Clear();
            for (int i = 0; i < poolSize; i++)
            {
                enemyPool.Add(enemyTypes[Random.Range(0, enemyTypes.Length)]);
            }
        }
        while (poolSize > 1 && enemyTypes.Length > 1 && HasOnlyOneType());
    }

    private bool HasOnlyOneType()
    {
        for (int i = 1; i < enemyPool.Count; i++)
        {
            if (enemyPool[i] != enemyPool[0])
            {
                return false;
            }
        }

        return true;
    }

    private void SpawnWave()
    {
        int groupCount = Random.Range(minGroupsPerWave, maxGroupsPerWave + 1);
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

        Vector3 trainPos = train.transform.position;
        Vector3 groupCenter = new Vector3(trainPos.x + side * Random.Range(20f, 100f), prefab.SpawnHeight, trainPos.z + Random.Range(-100f, -250f));

        int groupSize = Random.Range(prefab.MinGroupSize, prefab.MaxGroupSize + 1);
        for (int i = 0; i < groupSize; i++)
        {
            Vector2 scatter = Random.insideUnitCircle * groupSpread;
            Vector3 spawnPoint = groupCenter + new Vector3(scatter.x, 0f, scatter.y);
            EnemyBase enemy = Instantiate(prefab, spawnPoint, Quaternion.identity);
            enemy.SetFormation(i, side, groupCenter);
            enemy.NetworkObject.Spawn(true);
        }
    }
}
