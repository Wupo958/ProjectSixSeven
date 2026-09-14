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
 
    public void Launch(Vector3 direction, float speed, float maxRange, float damage)
    {
        _direction = direction.normalized;
        _speed = speed;
        _maxRange = maxRange;
        _damage = damage;
    }
 
    private void Update()
    {
        if (!IsServer || _speed <= 0f) return;
 
        float step = _speed * Time.deltaTime;
 
        if (Physics.Raycast(transform.position, _direction, out RaycastHit hit, step))
        {
            IDamageable target = hit.collider.GetComponentInParent<IDamageable>();
            target?.ApplyDamage(_damage, hit.point);
 
            if (NetworkObject.IsSpawned) NetworkObject.Despawn();
            return;
        }
 
        transform.position += _direction * step;
 
        _travelled += step;
        if (_travelled > _maxRange && NetworkObject.IsSpawned) NetworkObject.Despawn();
    }
}
