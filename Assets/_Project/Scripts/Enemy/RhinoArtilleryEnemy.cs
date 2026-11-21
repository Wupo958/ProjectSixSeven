using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class RhinoArtilleryEnemy : EnemyBase
{
    private enum RhinoState
    {
        Positioning,
        Bombarding,
        Charging
    }

    [SerializeField] private MortarShell shellPrefab;
    [SerializeField] private GameObject mortarVisual;
    [SerializeField] private float shellDamage = 60f;
    [SerializeField] private float shellBlastRadius = 1.25f;
    [SerializeField] private float shellFlightSeconds = 3f;
    [SerializeField] private float secondsBetweenShots = 8f;

    [SerializeField] private float inaccuracyRadiusPerDamage = 0.1f;
    [SerializeField] private float maxInaccuracyRadius = 15f;

    [SerializeField] private float artilleryDistance = 100f;
    [SerializeField] private float arriveDistance = 10f;

    [SerializeField] [Range(0f, 1f)] private float chargeBelowHealthFraction = 0.4f;
    [SerializeField] private float chargeSpeed = 40f;
    [SerializeField] private float ramDamage = 300f;
    [SerializeField] private float ramReachDistance = 6f;
    [SerializeField] private float trampleRadius = 4f;

    private NetworkVariable<bool> isMortarDestroyed = new NetworkVariable<bool>();

    private RhinoState state = RhinoState.Positioning;
    private float nextShotTime;
    private float damageSinceLastShot;
    private HashSet<EnemyBase> trampled = new HashSet<EnemyBase>();

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        isMortarDestroyed.OnValueChanged += OnMortarDestroyedChanged;
        mortarVisual.SetActive(!isMortarDestroyed.Value);

        if (IsServer)
        {
            targetIndex = Train.HighestHpTarget();
        }
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        isMortarDestroyed.OnValueChanged -= OnMortarDestroyedChanged;
    }

    protected override void Think()
    {
        if (!HasTarget)
        {
            targetIndex = Train.HighestHpTarget();
            if (!HasTarget)
            {
                return;
            }
        }

        switch (state)
        {
            case RhinoState.Positioning:
                if (FlatDistanceTo(ArtilleryStation()) <= arriveDistance)
                {
                    nextShotTime = Time.time + secondsBetweenShots;
                    state = RhinoState.Bombarding;
                }
                break;

            case RhinoState.Bombarding:
                if (Time.time >= nextShotTime)
                {
                    FireShell();
                    nextShotTime = Time.time + secondsBetweenShots;
                }
                break;

            case RhinoState.Charging:
                TrampleEnemies();
                if (FlatDistanceTo(Train.TargetPosition(targetIndex)) <= ramReachDistance)
                {
                    RamTarget();
                }
                break;
        }
    }

    protected override void Move()
    {
        Vector3 desired = DesiredVelocity(transform.position);

        if (HasTarget && state == RhinoState.Charging)
        {
            Vector3 toTarget = Train.TargetPosition(targetIndex) - transform.position;
            toTarget.y = 0f;
            desired = TrainVelocity + toTarget.normalized * chargeSpeed * SpeedFactor;
        }
        else if (HasTarget)
        {
            Vector3 station = ArtilleryStation();
            station.y = transform.position.y;
            desired = DesiredVelocity(station);
        }

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
        damageSinceLastShot += amount;

        if (state != RhinoState.Charging && HealthFraction <= chargeBelowHealthFraction)
        {
            StartCharge();
        }
    }

    private void FireShell()
    {
        float inaccuracy = Mathf.Min(maxInaccuracyRadius, damageSinceLastShot * inaccuracyRadiusPerDamage);
        Vector2 miss = Random.insideUnitCircle * inaccuracy;

        Vector3 impactPoint = TargetPosition + TrainVelocity * shellFlightSeconds;
        impactPoint += new Vector3(miss.x, 0f, miss.y);

        MortarShell shell = Instantiate(shellPrefab, muzzle.position, Quaternion.identity);
        shell.NetworkObject.Spawn(true);
        shell.Launch(MortarShell.LaunchVelocityFor(muzzle.position, impactPoint, shellFlightSeconds), shellDamage, shellBlastRadius, transform);

        damageSinceLastShot = 0f;
    }

    private void StartCharge()
    {
        state = RhinoState.Charging;
        armor = 0f;
        isMortarDestroyed.Value = true;
    }

    [ContextMenu("Debug: Start Charge")]
    private void DebugStartCharge()
    {
        if (!IsServer || state == RhinoState.Charging)
        {
            return;
        }

        StartCharge();
    }

    private void TrampleEnemies()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, trampleRadius);
        foreach (Collider hit in hits)
        {
            EnemyBase enemy = hit.GetComponentInParent<EnemyBase>();
            if (enemy != null && enemy != this && trampled.Add(enemy))
            {
                enemy.ApplyDamage(ramDamage, hit.transform.position, PlayerHealth.EnvironmentAttacker);
            }
        }
    }

    private void RamTarget()
    {
        Train.DamageTarget(targetIndex, Train.TargetPosition(targetIndex), Mathf.RoundToInt(ramDamage));
        NetworkObject.Despawn();
    }

    private Vector3 ArtilleryStation()
    {
        return ParallelStation(targetIndex, artilleryDistance, SpawnSide);
    }

    private float FlatDistanceTo(Vector3 point)
    {
        Vector3 offset = point - transform.position;
        offset.y = 0f;
        return offset.magnitude;
    }

    private void OnMortarDestroyedChanged(bool wasDestroyed, bool isDestroyed)
    {
        mortarVisual.SetActive(!isDestroyed);
    }
}
