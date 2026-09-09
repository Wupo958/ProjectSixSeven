using Unity.Netcode;
using UnityEngine;

public class BasicProjectileScript : NetworkBehaviour
{
    [SerializeField] private float _speed = 30f;
    [SerializeField] private int _damage = 1;

    public float Speed => _speed;

    private Vector3 _direction;
    private float _maxRange = 200f;
    private float _travelled;

    public void Launch(Vector3 direction, float maxRange)
    {
        _direction = direction.normalized;
        _maxRange = maxRange;
    }

    // Update is called once per frame
    void Update()
    {
        // the server flies the bullet, clients just follow it with the NetworkTransform
        if (!IsServer)
        {
            return;
        }

        float step = _speed * Time.deltaTime;

        // raycast the way we are about to move so we cant fly through the train
        if (Physics.Raycast(transform.position, _direction, out RaycastHit hit, step))
        {
            Hit(hit.collider, hit.point);
            return;
        }

        transform.position += _direction * step;

        _travelled += step;
        if (_travelled > _maxRange)
        {
            NetworkObject.Despawn();
        }
    }

    private void Hit(Collider other, Vector3 hitPos)
    {
        Carriage carriage = other.GetComponentInParent<Carriage>();
        if (carriage != null)
        {
            carriage.TakeDamage(_damage, hitPos);
        }

        NetworkObject.Despawn();
    }
}
