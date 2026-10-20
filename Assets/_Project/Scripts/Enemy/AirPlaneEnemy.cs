using Unity.Netcode;
using UnityEngine;

public class AirPlaneEnemy : EnemyBase
{
    [SerializeField] private float orbitRadius = 60f;
    [SerializeField] private float orbitHeight = 40f;
    [SerializeField] private float orbitDegreesPerSecond = 15f;

    private float orbitAngle;
    private float orbitDirection;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (!IsServer)
        {
            return;
        }

        Vector3 offset = transform.position - Train.CarryReference.position;
        offset.y = 0f;
        orbitAngle = Mathf.Atan2(offset.z, offset.x) * Mathf.Rad2Deg;
        orbitDirection = 1f;
        if (Vector3.Dot(offset, TrainRight) < 0f)
        {
            orbitDirection = -1f;
        }
    }

    protected override void Move()
    {
        orbitAngle += orbitDirection * orbitDegreesPerSecond * Time.fixedDeltaTime;

        float radians = orbitAngle * Mathf.Deg2Rad;
        Vector3 ring = new Vector3(Mathf.Cos(radians), 0f, Mathf.Sin(radians)) * orbitRadius;
        Vector3 station = Train.CarryReference.position + Vector3.up * orbitHeight + ring;

        ApplySteering(DesiredVelocity(station));

        if (rb.linearVelocity.sqrMagnitude > 0.01f)
        {
            rb.MoveRotation(Quaternion.LookRotation(rb.linearVelocity));
        }
    }
}
