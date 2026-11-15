using System;
using Unity.Collections;
using Unity.Netcode;

public struct RouteOption : INetworkSerializeByMemcpy, IEquatable<RouteOption>
{
    public const int MaxEnemyPoolSize = 5;

    public int Seed;
    public float Length;
    public float WaveInterval;
    public int WaveSize;
    public FixedList32Bytes<int> EnemyPool;
    public int Payout;

    public bool IsValid => Length > 0f;

    public float WavesPerMinute
    {
        get
        {
            if (WaveInterval <= 0f)
            {
                return 0f;
            }

            return 60f / WaveInterval;
        }
    }

    public int DistinctEnemyTypes
    {
        get
        {
            int distinct = 0;
            for (int i = 0; i < EnemyPool.Length; i++)
            {
                bool seenBefore = false;
                for (int j = 0; j < i; j++)
                {
                    if (EnemyPool[j] == EnemyPool[i])
                    {
                        seenBefore = true;
                        break;
                    }
                }

                if (!seenBefore)
                {
                    distinct++;
                }
            }

            return distinct;
        }
    }

    public bool Equals(RouteOption other)
    {
        return Seed == other.Seed
            && Length.Equals(other.Length)
            && WaveInterval.Equals(other.WaveInterval)
            && WaveSize == other.WaveSize
            && EnemyPool.Equals(other.EnemyPool)
            && Payout == other.Payout;
    }

    public override bool Equals(object obj)
    {
        if (obj is RouteOption other)
        {
            return Equals(other);
        }

        return false;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Seed, Length, WaveInterval, WaveSize, EnemyPool.GetHashCode(), Payout);
    }
}
