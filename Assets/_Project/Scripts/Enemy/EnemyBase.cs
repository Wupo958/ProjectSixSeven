using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public abstract class EnemyBase : NetworkBehaviour, IDamageable
{
    [SerializeField] protected Transform muzzle;
    [SerializeField] private float targetAimHeight = 1.5f;

    [SerializeField] protected float maxHealth = 3f;
    [SerializeField] [Range(0f, 100f)] protected float armor = 0f;

    [SerializeField] protected float acceleration = 40f;
    [SerializeField] protected float maxSpeed = 60f;
    [SerializeField] protected float stationGain = 1f;

    [SerializeField] private int minGroupSize = 1;
    [SerializeField] private int maxGroupSize = 1;
    [SerializeField] private float spawnHeight = 0f;

    [SerializeField] private string displayName;

    private float health;
    private ulong lastAttackerClientId = WeaponBase.NoOccupant;
    private int groupIndex;
    private int groupSeed;
    private float spawnSide = 1f;
    private Vector3 groupCenter;
    private StatusEffectReceiver status;

    protected Rigidbody rb;
    protected int targetIndex = -1;

    private static List<EnemyBase> active = new List<EnemyBase>();

    public string DisplayName
    {
        get
        {
            if (string.IsNullOrWhiteSpace(displayName))
            {
                return name;
            }

            return displayName;
        }
    }

    public int MinGroupSize
    {
        get
        {
            return minGroupSize;
        }
    }

    public int MaxGroupSize
    {
        get
        {
            return maxGroupSize;
        }
    }

    public float SpawnHeight
    {
        get
        {
            return spawnHeight;
        }
    }

    protected int GroupIndex => groupIndex;
    protected int GroupSeed => groupSeed;
    protected float SpawnSide => spawnSide;
    protected Vector3 GroupCenter => groupCenter;

    protected float HealthFraction => health / maxHealth;

    protected float SpeedFactor
    {
        get
        {
            if (status == null)
            {
                return 1f;
            }

            return status.Mods.SpeedMultiplier;
        }
    }

    protected TrainHealth Train => TrainHealth.Instance;
    protected Vector3 TrainVelocity => Train.Velocity;

    protected Vector3 TrainHeading
    {
        get
        {
            if (TrainVelocity.sqrMagnitude > 1f)
            {
                return TrainVelocity.normalized;
            }

            return Train.transform.forward;
        }
    }

    protected Vector3 TrainRight => Vector3.Cross(Vector3.up, TrainHeading);

    protected bool HasTarget => targetIndex >= 0 && Train.IsTargetAlive(targetIndex);

    protected Vector3 TargetPosition
    {
        get
        {
            if (HasTarget)
            {
                return Train.TargetPosition(targetIndex) + Vector3.up * targetAimHeight;
            }

            return Train.AimTarget.position;
        }
    }

    protected Vector3 PredictedTargetPosition(float seconds)
    {
        return Train.PredictTargetPosition(targetIndex, seconds) + Vector3.up * targetAimHeight;
    }

    public void SetGroup(int index, int seed, float side, Vector3 center)
    {
        groupIndex = index;
        groupSeed = seed;
        spawnSide = side;
        groupCenter = center;
    }

    public override void OnNetworkSpawn()
    {
        active.Add(this);

        if (IsServer)
        {
            health = maxHealth;
        }
    }

    public override void OnNetworkDespawn()
    {
        active.Remove(this);
    }

    public static void DespawnAll()
    {
        for (int i = active.Count - 1; i >= 0; i--)
        {
            EnemyBase enemy = active[i];
            if (enemy != null && enemy.IsSpawned)
            {
                enemy.NetworkObject.Despawn();
            }
        }

        active.Clear();
    }

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody>();
        status = GetComponent<StatusEffectReceiver>();
    }

    public void ApplyDamage(float amount, Vector3 hitPoint, ulong attackerClientId)
    {
        if (!IsServer || !IsSpawned)
        {
            return;
        }

        lastAttackerClientId = attackerClientId;
        float damageAfterArmor = amount * (1f - armor / 100f);
        health -= damageAfterArmor;

        if (health <= 0f)
        {
            Die();
        }
        else
        {
            OnDamaged(damageAfterArmor);
        }
    }

    protected virtual void OnDamaged(float amount)
    {
    }

    protected virtual void Die()
    {
        if (GameSession.Instance != null)
        {
            GameSession.Instance.AddKill(lastAttackerClientId);
        }

        NetworkObject.Despawn();
    }

    private void Update()
    {
        if (!IsServer || Train == null)
        {
            return;
        }

        Think();
    }

    private void FixedUpdate()
    {
        if (!IsServer || Train == null)
        {
            return;
        }

        Move();
    }

    protected abstract void Think();

    protected abstract void Move();

    protected Vector3 DesiredVelocity(Vector3 stationPoint)
    {
        Vector3 toStation = stationPoint - transform.position;
        return Vector3.ClampMagnitude(TrainVelocity + toStation * stationGain, maxSpeed * SpeedFactor);
    }

    protected void ApplySteering(Vector3 desiredVelocity)
    {
        Vector3 steering = Vector3.ClampMagnitude(desiredVelocity - rb.linearVelocity, acceleration * SpeedFactor);
        rb.linearVelocity += steering * Time.fixedDeltaTime;
    }

    protected Vector3 SeparationVelocity(float radius, float strength)
    {
        Vector3 push = Vector3.zero;
        foreach (EnemyBase other in active)
        {
            if (other == this)
            {
                continue;
            }

            Vector3 away = transform.position - other.transform.position;
            away.y = 0f;
            float distance = away.magnitude;
            if (distance > 0.01f && distance < radius)
            {
                push += away / distance * ((radius - distance) / radius);
            }
        }

        return push * strength;
    }

    protected Vector3 ParallelStation(int target, float distance, float side)
    {
        return Train.TargetPosition(target) + TrainRight * side * distance;
    }

    protected void FireBullet(GameObject bulletPrefab, float coneAngle, float bulletDamage, float bulletRange)
    {
        BasicProjectileScript prefabScript = bulletPrefab.GetComponent<BasicProjectileScript>();
        Vector3 aimPoint = PredictAimPoint(muzzle.position, TargetPosition, prefabScript.Speed);
        Vector3 direction = RandomizeInCone((aimPoint - muzzle.position).normalized, coneAngle);

        GameObject bullet = Instantiate(bulletPrefab, muzzle.position, Quaternion.LookRotation(direction));
        bullet.GetComponent<NetworkObject>().Spawn(destroyWithScene: true);
        bullet.GetComponent<BasicProjectileScript>().Launch(direction, bulletRange, bulletDamage);
    }

    protected Vector3 PredictAimPoint(Vector3 from, Vector3 targetPosition, float projectileSpeed)
    {
        Vector3 aimPoint = targetPosition;

        for (int i = 0; i < 2; i++)
        {
            float travelTime = Vector3.Distance(from, aimPoint) / projectileSpeed;
            aimPoint = targetPosition + TrainVelocity * travelTime;
        }

        return aimPoint;
    }

    protected Vector3 RandomizeInCone(Vector3 direction, float coneAngle)
    {
        float half = coneAngle / 2f;
        Quaternion spread = Quaternion.Euler(Random.Range(-half, half), Random.Range(-half, half), 0f);
        return spread * direction;
    }
}
