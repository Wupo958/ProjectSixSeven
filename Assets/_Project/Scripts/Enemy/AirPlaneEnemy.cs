using UnityEngine;

public class AirPlaneEnemy : EnemyBase
{
    [SerializeField] private float orbitRadius = 60f;
    [SerializeField] private float orbitHeight = 40f;
    [SerializeField] private float orbitDegreesPerSecond = 15f;

    [SerializeField] private float wedgeDegreesBehind = 8f;
    [SerializeField] private float wedgeSideSpacing = 10f;
    [SerializeField] private float wedgeHeightStep = 3f;

    private float orbitAngle;
    private float orbitDirection;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (!IsServer)
        {
            return;
        }

        Vector3 offset = GroupCenter - Train.CarryReference.position;
        orbitAngle = Mathf.Atan2(offset.z, offset.x) * Mathf.Rad2Deg;
        orbitDirection = FormationSide;
    }

    protected override void Move()
    {
        orbitAngle += orbitDirection * orbitDegreesPerSecond * Time.fixedDeltaTime;

        ApplySteering(DesiredVelocity(WedgeStation()));

        if (rb.linearVelocity.sqrMagnitude > 0.01f)
        {
            rb.MoveRotation(Quaternion.LookRotation(rb.linearVelocity));
        }
    }

    private Vector3 WedgeStation()
    {
        float slotAngle = orbitAngle - orbitDirection * FormationRank * wedgeDegreesBehind;
        float slotRadius = orbitRadius + StaggeredRank * wedgeSideSpacing;
        float slotHeight = orbitHeight + FormationRank * wedgeHeightStep;

        float radians = slotAngle * Mathf.Deg2Rad;
        Vector3 ring = new Vector3(Mathf.Cos(radians), 0f, Mathf.Sin(radians)) * slotRadius;
        return Train.CarryReference.position + Vector3.up * slotHeight + ring;
    }
}
