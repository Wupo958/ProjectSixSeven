using UnityEngine;
using UnityEngine.Serialization;

public class GnuRiderEnemy : EnemyBase
{
    private enum RiderState
    {
        Approaching,
        Repositioning,
        Attacking,
        Evading
    }

    [SerializeField] [FormerlySerializedAs("projectilePrefab")] private GameObject pelletPrefab;
    [SerializeField] private int pelletsPerShot = 5;
    [SerializeField] private float pelletDamage = 4f;
    [SerializeField] private float pelletSpreadAngle = 12f;
    [SerializeField] private float pelletRange = 80f;
    [SerializeField] private float secondsBetweenShots = 4f;
    [SerializeField] private int shotsPerTarget = 3;

    [SerializeField] private float minAttackDistance = 25f;
    [SerializeField] private float maxAttackDistance = 50f;
    [SerializeField] private float wagonLengthSpread = 5f;
    [SerializeField] private float gangSpacing = 6f;
    [SerializeField] private float arriveDistance = 4f;
    [SerializeField] private float repositionTimeoutSeconds = 4f;
    [SerializeField] private float separationRadius = 10f;
    [SerializeField] private float separationStrength = 15f;

    [SerializeField] private float evadeSeconds = 3f;
    [SerializeField] private float evadeWeaveDistance = 8f;
    [SerializeField] private float evadeWeaveSpeed = 3f;

    private RiderState state = RiderState.Approaching;
    private float stationDistance;
    private float stationAlong;
    private float nextShotTime;
    private int shotsFired;
    private float evadeEndTime;
    private float evadeWeavePhase;
    private float repositionEndTime;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (!IsServer)
        {
            return;
        }

        targetIndex = GroupTarget();
    }

    protected override void Think()
    {
        if (!HasTarget)
        {
            LoseTarget();
            if (!HasTarget)
            {
                return;
            }
        }

        switch (state)
        {
            case RiderState.Approaching:
                if (IsWithinMaxRange())
                {
                    ChooseNextTarget();
                }
                break;

            case RiderState.Repositioning:
                if (IsAtStation() || Time.time >= repositionEndTime)
                {
                    shotsFired = 0;
                    state = RiderState.Attacking;
                }
                break;

            case RiderState.Attacking:
                TryShoot();
                break;

            case RiderState.Evading:
                if (Time.time >= evadeEndTime)
                {
                    ChooseNextTarget();
                }
                break;
        }
    }

    protected override void Move()
    {
        Vector3 station = transform.position;
        if (HasTarget)
        {
            station = CurrentStation();
        }

        station.y = transform.position.y;

        Vector3 desired = DesiredVelocity(station) + SeparationVelocity(separationRadius, separationStrength);
        desired.y = rb.linearVelocity.y;
        ApplySteering(desired);

        Vector3 flatVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        if (flatVelocity.sqrMagnitude > 0.01f)
        {
            rb.MoveRotation(Quaternion.LookRotation(flatVelocity));
        }
    }

    protected override void OnDamaged(float amount)
    {
        if (state != RiderState.Evading)
        {
            SwitchToNeighbourTarget();
            stationDistance = Random.Range(minAttackDistance, maxAttackDistance);
            evadeWeavePhase = Random.Range(0f, Mathf.PI * 2f);
        }

        state = RiderState.Evading;
        evadeEndTime = Time.time + evadeSeconds;
    }

    private void TryShoot()
    {
        if (Time.time < nextShotTime)
        {
            return;
        }

        for (int i = 0; i < pelletsPerShot; i++)
        {
            FireBullet(pelletPrefab, pelletSpreadAngle, pelletDamage, pelletRange);
        }

        nextShotTime = Time.time + secondsBetweenShots;
        shotsFired++;

        if (shotsFired >= shotsPerTarget)
        {
            ChooseNextTarget();
        }
    }

    private void ChooseNextTarget()
    {
        int candidate = targetIndex + Random.Range(-1, 2);
        if (Train.IsTargetAlive(candidate))
        {
            targetIndex = candidate;
        }

        stationDistance = Random.Range(minAttackDistance, maxAttackDistance);
        stationAlong = Random.Range(-wagonLengthSpread, wagonLengthSpread);
        repositionEndTime = Time.time + repositionTimeoutSeconds;
        state = RiderState.Repositioning;
    }

    private void SwitchToNeighbourTarget()
    {
        int direction = 1;
        if (Random.value < 0.5f)
        {
            direction = -1;
        }

        if (Train.IsTargetAlive(targetIndex + direction))
        {
            targetIndex += direction;
        }
        else if (Train.IsTargetAlive(targetIndex - direction))
        {
            targetIndex -= direction;
        }
    }

    private void LoseTarget()
    {
        targetIndex = Train.NearestTarget(transform.position);

        if (state == RiderState.Repositioning || state == RiderState.Attacking)
        {
            ChooseNextTarget();
        }
    }

    private int GroupTarget()
    {
        int targetableCount = 0;
        for (int i = 0; i < Train.TargetCount; i++)
        {
            if (Train.IsTargetAlive(i))
            {
                targetableCount++;
            }
        }

        if (targetableCount == 0)
        {
            return -1;
        }

        int remaining = GroupSeed % targetableCount;
        for (int i = 0; i < Train.TargetCount; i++)
        {
            if (!Train.IsTargetAlive(i))
            {
                continue;
            }

            if (remaining == 0)
            {
                return i;
            }

            remaining--;
        }

        return -1;
    }

    private Vector3 CurrentStation()
    {
        if (state == RiderState.Approaching)
        {
            return ParallelStation(targetIndex, maxAttackDistance, SpawnSide) - TrainHeading * GroupIndex * gangSpacing;
        }

        float distance = stationDistance;
        if (state == RiderState.Evading)
        {
            distance += Mathf.Sin(Time.time * evadeWeaveSpeed + evadeWeavePhase) * evadeWeaveDistance;
        }

        return ParallelStation(targetIndex, distance, SpawnSide) + TrainHeading * stationAlong;
    }

    private bool IsWithinMaxRange()
    {
        Vector3 offset = Train.TargetPosition(targetIndex) - transform.position;
        offset.y = 0f;
        return offset.magnitude <= maxAttackDistance;
    }

    private bool IsAtStation()
    {
        Vector3 offset = CurrentStation() - transform.position;
        offset.y = 0f;
        return offset.magnitude <= arriveDistance;
    }
}
