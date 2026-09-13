using Unity.Netcode;
using UnityEngine;

public class GroundJeepEnemy : EnemyBase
{
    [SerializeField] private float sideOffset = 25f;
    [SerializeField] private float forwardOffset = 0f;

    private float side;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (!IsServer)
        {
            return;
        }

        Vector3 toJeep = transform.position - Train.transform.position;
        side = 1f;
        if (Vector3.Dot(toJeep, TrainRight) < 0f)
        {
            side = -1f;
        }
    }

    protected override void Move()
    {
        Vector3 desired = DesiredVelocity(StationPoint());
        desired.y = rb.linearVelocity.y;
        ApplySteering(desired);

        Vector3 flatVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        if (flatVelocity.sqrMagnitude > 0.01f)
        {
            rb.MoveRotation(Quaternion.LookRotation(flatVelocity));
        }
    }

    private Vector3 StationPoint()
    {
        return Train.transform.position + TrainRight * side * sideOffset + TrainHeading * forwardOffset;
    }
}
