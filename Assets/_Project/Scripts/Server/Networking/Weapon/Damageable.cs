using Unity.Netcode;
using UnityEngine;
 
//anything weapons can hurt
public interface IDamageable
{
    void ApplyDamage(float amount, Vector3 hitPoint);
}
 
//minimal enemy health so the cannon has something to kill. TODO: Maybe delete/replace later?
public class EnemyHealth : NetworkBehaviour, IDamageable
{
    [SerializeField] private float _maxHealth = 100f;
 
    private readonly NetworkVariable<float> _health = new NetworkVariable<float>();
 
    public override void OnNetworkSpawn()
    {
        if (IsServer) _health.Value = _maxHealth;
    }
 
    public void ApplyDamage(float amount, Vector3 hitPoint)
    {
        if (!IsServer) return;
 
        _health.Value -= amount;
        if (_health.Value <= 0f && NetworkObject.IsSpawned)
        {
            NetworkObject.Despawn();
        }
    }
}
 
