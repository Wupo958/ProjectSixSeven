using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

[Serializable]
public sealed class RouteGenerator
{
    [Header("Station options")]
    [Tooltip("Track length in metres (min, max).")]
    [SerializeField] private Vector2 _lengthRange = new Vector2(1000f, 10000f);

    [Tooltip("Seconds between two waves (min, max). A shorter interval means a higher wave frequency.")]
    [SerializeField] private Vector2 _waveIntervalRange = new Vector2(60f, 120f);

    [Tooltip("Enemy groups per wave (min, max).")]
    [SerializeField] private Vector2Int _waveSizeRange = new Vector2Int(1, 5);

    [Tooltip("Slots in a level's enemy pool (min, max). The same type may fill several slots.")]
    [SerializeField] private Vector2Int _enemyPoolSizeRange = new Vector2Int(1, 5);

    [Header("First level of a run")]
    [SerializeField] private float _startLength = 2500f;
    [SerializeField] private float _startWaveInterval = 120f;
    [SerializeField] private int _startWaveSize = 1;
    [SerializeField] private int _startEnemyPoolSize = 1;

    public RouteOption CreateStart(System.Random rng, int enemyTypeCount)
    {
        return Create(rng, _startLength, _startWaveInterval, _startWaveSize, _startEnemyPoolSize, enemyTypeCount);
    }

    public List<RouteOption> CreateOptions(int count, System.Random rng, int enemyTypeCount)
    {
        count = Mathf.Max(1, count);

        List<RouteOption> options = new List<RouteOption>(count);
        for (int i = 0; i < count; i++)
        {
            float length = Mathf.Round(Lerp(rng, _lengthRange) / 100f) * 100f;
            float waveInterval = Mathf.Round(Lerp(rng, _waveIntervalRange));
            int waveSize = rng.Next(_waveSizeRange.x, _waveSizeRange.y + 1);
            int poolSize = rng.Next(_enemyPoolSizeRange.x, _enemyPoolSizeRange.y + 1);

            options.Add(Create(rng, length, waveInterval, waveSize, poolSize, enemyTypeCount));
        }

        return options;
    }

    public static FixedList32Bytes<int> RollEnemyPool(System.Random rng, int poolSize, int enemyTypeCount)
    {
        FixedList32Bytes<int> pool = new FixedList32Bytes<int>();
        if (enemyTypeCount <= 0)
        {
            return pool;
        }

        poolSize = Mathf.Clamp(poolSize, 1, RouteOption.MaxEnemyPoolSize);

        do
        {
            pool.Clear();
            for (int i = 0; i < poolSize; i++)
            {
                pool.Add(rng.Next(0, enemyTypeCount));
            }
        }
        while (poolSize > 1 && enemyTypeCount > 1 && HasOnlyOneType(pool));

        return pool;
    }

    public static int CalculateBasePayout(RouteOption route, int cargoWagons)
    {
        float lengthKm = route.Length / 1000f;
        int poolFactor = route.EnemyPool.Length + route.DistinctEnemyTypes + cargoWagons;
        return Mathf.FloorToInt(lengthKm * route.WavesPerMinute * poolFactor);
    }

    private static RouteOption Create(System.Random rng, float length, float waveInterval, int waveSize, int poolSize, int enemyTypeCount)
    {
        RouteOption route = new RouteOption
        {
            Seed = rng.Next(),
            Length = length,
            WaveInterval = waveInterval,
            WaveSize = Mathf.Max(1, waveSize),
            EnemyPool = RollEnemyPool(rng, poolSize, enemyTypeCount)
        };

        route.Payout = CalculateBasePayout(route, 0);
        return route;
    }

    private static bool HasOnlyOneType(FixedList32Bytes<int> pool)
    {
        for (int i = 1; i < pool.Length; i++)
        {
            if (pool[i] != pool[0])
            {
                return false;
            }
        }

        return true;
    }

    private static float Lerp(System.Random rng, Vector2 range)
    {
        return Mathf.Lerp(range.x, range.y, (float)rng.NextDouble());
    }
}
