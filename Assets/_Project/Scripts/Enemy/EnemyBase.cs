using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public abstract class EnemyBase : NetworkBehaviour, IDamageable
{
    [SerializeField] protected Transform muzzle;
    [SerializeField] private GameObject projectilePrefab;

    [SerializeField] protected float maxHealth = 3f;
    [SerializeField] [Range(0f, 100f)] private float armor = 0f;

    [SerializeField] protected float damage = 3f;
    [SerializeField] protected float maxRange = 200f;
    [SerializeField] protected float attackInterval = 1f;
    [SerializeField] protected float firstAttackDelay = 1f;
    [SerializeField] protected float coneAngle = 30f;

    [SerializeField] protected float acceleration = 40f;
    [SerializeField] protected float maxSpeed = 60f;
    [SerializeField] protected float stationGain = 1f;

    [SerializeField] private int minGroupSize = 1;
    [SerializeField] private int maxGroupSize = 1;
    [SerializeField] private float spawnHeight = 0f;

    private float health;
    private ulong lastAttackerClientId = WeaponBase.NoOccupant;
    private Vector3 lastTrainPos;
    private Vector3 trainVelocity;
    private bool hasLastTrainPos;
    private int formationSlot;
    private float formationSide = 1f;
    private Vector3 groupCenter;
    protected Rigidbody rb;

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

    protected float FormationSide => formationSide;
    protected Vector3 GroupCenter => groupCenter;

    protected int FormationRank
    {
        get
        {
            return (formationSlot + 1) / 2;
        }
    }

    protected float StaggeredRank
    {
        get
        {
            if (formationSlot % 2 == 0)
            {
                return -FormationRank;
            }

            return FormationRank;
        }
    }

    protected TrainHealth Train => TrainHealth.Instance;
    protected Vector3 TrainVelocity => trainVelocity;

    protected Vector3 TrainHeading
    {
        get
        {
            if (trainVelocity.sqrMagnitude > 1f)
            {
                return trainVelocity.normalized;
            }

            return Train.transform.forward;
        }
    }

    protected Vector3 TrainRight => Vector3.Cross(Vector3.up, TrainHeading);

    private static readonly List<EnemyBase> active = new List<EnemyBase>();

    public void SetFormation(int slot, float side, Vector3 center)
    {
        formationSlot = slot;
        formationSide = side;
        groupCenter = center;
    }

    public override void OnNetworkSpawn()
    {
        active.Add(this);

        if (IsServer)
        {
            health = maxHealth;
            InvokeRepeating(nameof(TryAttack), firstAttackDelay, attackInterval);
        }
    }

    public override void OnNetworkDespawn()
    {
        active.Remove(this);
        CancelInvoke();
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
    }

    protected virtual void Die()
    {
        if (GameSession.Instance != null)
        {
            GameSession.Instance.AddKill(lastAttackerClientId);
        }


        NetworkObject.Despawn();
    }

    private void TryAttack()
    {
        if (!CanAttack())
        {
            return;
        }

        Attack();
    }

    protected virtual void Update()
    {
        if (!IsServer || Time.deltaTime <= 0f)
        {
            return;
        }

        Vector3 pos = Train.CarryReference.transform.position;
        if (hasLastTrainPos)
        {
            trainVelocity = (pos - lastTrainPos) / Time.deltaTime;
        }

        lastTrainPos = pos;
        hasLastTrainPos = true;
    }

    protected virtual void FixedUpdate()
    {
        if (!IsServer)
        {
            return;
        }
        Move();
    }

    protected virtual void Attack()
    {
        float projectileSpeed = projectilePrefab.GetComponent<BasicProjectileScript>().Speed;

        Vector3 direction = (PredictAimPoint(muzzle.position, projectileSpeed) - muzzle.position).normalized;
        direction = RandomizeInCone(direction, coneAngle);

        GameObject projectile = Instantiate(projectilePrefab, muzzle.position, Quaternion.LookRotation(direction));
        projectile.GetComponent<NetworkObject>().Spawn(destroyWithScene: true);
        projectile.GetComponent<BasicProjectileScript>().Launch(direction, maxRange, damage);
    }

    protected abstract void Move();

    protected virtual bool CanAttack()
    {
        return Vector3.Distance(muzzle.position, Train.AimTarget.transform.position) <= maxRange;
    }

    protected Vector3 DesiredVelocity(Vector3 stationPoint)
    {
        Vector3 toStation = stationPoint - transform.position;
        return Vector3.ClampMagnitude(trainVelocity + toStation * stationGain, maxSpeed);
    }

    protected void ApplySteering(Vector3 desiredVelocity)
    {
        Vector3 steering = Vector3.ClampMagnitude(desiredVelocity - rb.linearVelocity, acceleration);
        rb.linearVelocity += steering * Time.fixedDeltaTime;
    }

    protected Vector3 PredictAimPoint(Vector3 from, float projectileSpeed)
    {
        Vector3 trainPos = Train.AimTarget.transform.position;
        Vector3 aimPoint = trainPos;

        for (int i = 0; i < 2; i++)
        {
            float travelTime = Vector3.Distance(from, aimPoint) / projectileSpeed;
            aimPoint = trainPos + trainVelocity * travelTime;
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
