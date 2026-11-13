using UnityEngine;

public class GroundJeepEnemy : EnemyBase
{
    [SerializeField] private float sideOffset = 25f;
    [SerializeField] private float forwardOffset = 0f;
    [SerializeField] private float formationSpacing = 12f;

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
        float formationForward = forwardOffset + StaggeredRank * formationSpacing;
        return Train.CarryReference.position + TrainRight * FormationSide * sideOffset + TrainHeading * formationForward;
    }
}
