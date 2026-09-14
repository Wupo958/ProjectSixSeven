using UnityEngine;
 
//anything weapons can hurt
public interface IDamageable
{
    void ApplyDamage(float amount, Vector3 hitPoint);
}
