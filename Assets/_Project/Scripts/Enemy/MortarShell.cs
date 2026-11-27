using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class MortarShell : NetworkBehaviour, IDamageable
{
    [SerializeField] private float maxLifetimeSeconds = 15f;

    private Vector3 velocity;
    private float damage;
    private float blastRadius;
    private Transform shooter;
    private float despawnTime;
    private bool hasExploded;

    public void Launch(Vector3 launchVelocity, float blastDamage, float radius, Transform launchedBy)
    {
        velocity = launchVelocity;
        damage = blastDamage;
        blastRadius = radius;
        shooter = launchedBy;
        despawnTime = Time.time + maxLifetimeSeconds;
    }

    public static Vector3 LaunchVelocityFor(Vector3 from, Vector3 to, float flightSeconds)
    {
        return (to - from) / flightSeconds - 0.5f * Physics.gravity * flightSeconds;
    }

    private void Update()
    {
        if (!IsServer || hasExploded)
        {
            return;
        }

        if (Time.time >= despawnTime)
        {
            NetworkObject.Despawn();
            return;
        }

        velocity += Physics.gravity * Time.deltaTime;
        Vector3 step = velocity * Time.deltaTime;

        if (Physics.Raycast(transform.position, step.normalized, out RaycastHit hit, step.magnitude) && !IsOwnOrShooter(hit.collider))
        {
            Explode(hit.point);
            return;
        }

        transform.position += step;
        transform.rotation = Quaternion.LookRotation(velocity);
    }

    public void ApplyDamage(float amount, Vector3 hitPoint, ulong attackerClientId)
    {
        if (!IsServer || hasExploded)
        {
            return;
        }

        Explode(transform.position);
    }

    private void Explode(Vector3 point)
    {
        hasExploded = true;

        bool hasHitTrain = false;
        HashSet<IDamageable> damaged = new HashSet<IDamageable>();
        Collider[] hits = Physics.OverlapSphere(point, blastRadius);

        foreach (Collider hit in hits)
        {
            if (IsOwnOrShooter(hit))
            {
                continue;
            }

            IDamageable target = hit.GetComponentInParent<IDamageable>();
            if (target != null)
            {
                if (damaged.Add(target))
                {
                    target.ApplyDamage(damage, point, PlayerHealth.EnvironmentAttacker);
                }

                continue;
            }

            TrainHealth train = hit.GetComponentInParent<TrainHealth>();
            if (train != null && !hasHitTrain)
            {
                train.ReportHit(hit, point, Mathf.RoundToInt(damage));
                hasHitTrain = true;
            }
        }

        NetworkObject.Despawn();
    }

    private bool IsOwnOrShooter(Collider hit)
    {
        if (hit.transform.IsChildOf(transform))
        {
            return true;
        }

        return shooter != null && hit.transform.IsChildOf(shooter);
    }
}
