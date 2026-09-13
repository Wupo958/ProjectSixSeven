using Unity.Netcode;
using UnityEngine;

public abstract class EnemyBase : NetworkBehaviour
{
    [SerializeField] protected Transform muzzle;
    [SerializeField] private GameObject projectilePrefab;

    [SerializeField] protected int maxHealth = 3;

    [SerializeField] protected float maxRange = 200f;
    [SerializeField] protected float attackInterval = 1f;
    [SerializeField] protected float firstAttackDelay = 1f;
    [SerializeField] protected float coneAngle = 30f;

    [SerializeField] protected float acceleration = 40f;
    [SerializeField] protected float maxSpeed = 60f;
    [SerializeField] protected float stationGain = 1f;

    private int health;
    private Vector3 lastTrainPos;
    private Vector3 trainVelocity;
    private bool hasLastTrainPos;
    protected Rigidbody rb;

    protected Carriage Train => Carriage.Main;
    protected Vector3 TrainVelocity => trainVelocity;

    protected Vector3 TrainHeading
    {
        get
        {
            if (trainVelocity.sqrMagnitude > 1f)
            {
                return trainVelocity.normalized;
            }

            return Carriage.Main.transform.forward;
        }
    }

    protected Vector3 TrainRight => Vector3.Cross(Vector3.up, TrainHeading);

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            health = maxHealth;
            InvokeRepeating(nameof(TryAttack), firstAttackDelay, attackInterval);
        }
    }

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    public void TakeDamage(int amount)
    {
        if (!IsServer)
        {
            return;
        }

        health -= amount;
        if (health <= 0)
        {
            Die();
        }
    }

    protected virtual void Die()
    {
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

        Vector3 pos = Carriage.Main.transform.position;
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
        projectile.GetComponent<NetworkObject>().Spawn();
        projectile.GetComponent<BasicProjectileScript>().Launch(direction, maxRange);
    }

    protected abstract void Move();

    protected virtual bool CanAttack()
    {
        return Vector3.Distance(muzzle.position, Carriage.Main.transform.position) <= maxRange;
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
        Vector3 trainPos = Carriage.Main.transform.position;
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
