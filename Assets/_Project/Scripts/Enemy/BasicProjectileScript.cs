using Unity.Netcode;
using UnityEngine;
using UnityEngine.Serialization;

public class BasicProjectileScript : NetworkBehaviour
{
    [SerializeField] [FormerlySerializedAs("_speed")] private float speed = 30f;

    private Vector3 direction;
    private float maxRange = 200f;
    private float travelled;
    private float damage;

    public float Speed
    {
        get
        {
            return speed;
        }
    }

    public void Launch(Vector3 launchDirection, float launchMaxRange, float launchDamage)
    {
        direction = launchDirection.normalized;
        maxRange = launchMaxRange;
        damage = launchDamage;
    }

    private void Update()
    {
        if (!IsServer)
        {
            return;
        }

        float step = speed * Time.deltaTime;

        if (Physics.Raycast(transform.position, direction, out RaycastHit hit, step))
        {
            Hit(hit.collider, hit.point);
            return;
        }

        transform.position += direction * step;

        travelled += step;
        if (travelled > maxRange)
        {
            NetworkObject.Despawn();
        }
    }

    private void Hit(Collider other, Vector3 hitPos)
    {
        TrainHealth train = other.GetComponentInParent<TrainHealth>();
        if (train != null)
        {
            train.ReportHit(other, hitPos, Mathf.RoundToInt(damage));
        }

        NetworkObject.Despawn();
    }
}
