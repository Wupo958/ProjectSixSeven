using Unity.Netcode;
using UnityEngine;
 
//server-flown shell. Damage and speed come from the weapon that fired it
public sealed class WeaponProjectile : NetworkBehaviour
{
    private Vector3 _direction;
    private float _speed;
    private float _maxRange;
    private float _damage;
    private float _travelled;
    private Vector3 _velocity;
    private ulong shooterClientId = WeaponBase.NoOccupant;

    public void Launch(Vector3 direction, float speed, float maxRange, float damage, Vector3 inherited, ulong shooter)
    {
        shooterClientId = shooter;
        _velocity = direction.normalized * speed + inherited;
        _maxRange = maxRange;
        _damage = damage;
    }
 
    private void Update()
    {
        if (!IsServer || _velocity.sqrMagnitude <= 0f) return;

        Vector3 step = _velocity * Time.deltaTime;
        float dist = step.magnitude;
        Vector3 dir = step / dist;

        if (Physics.Raycast(transform.position, dir, out RaycastHit hit, dist))
        {
            IDamageable target = hit.collider.GetComponentInParent<IDamageable>();
            if (target != null)
            {
                target.ApplyDamage(_damage, hit.point, shooterClientId);
            }
            if (NetworkObject.IsSpawned) NetworkObject.Despawn();
            return;
        }

        transform.position += step;
        _travelled += dist;
        if (_travelled > _maxRange && NetworkObject.IsSpawned) NetworkObject.Despawn();
    }
}
