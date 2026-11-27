using UnityEngine;

//damage every Tick Interval, per stack. E.g. Burning. Works on anything that takes damage (enemies, players).
[CreateAssetMenu(menuName = "Status Effects/Damage Over Time", fileName = "Burning")]
public sealed class DamageOverTimeEffect : StatusEffect
{
    [Tooltip("Damage per tick, per stack.")]
    [SerializeField, Min(0f)] private float _damagePerTick = 10f;

    public override void Tick(StatusEffectReceiver target, int stacks, ulong source)
    {
        if (target.TryGetComponent(out IDamageable damageable))
            damageable.ApplyDamage(_damagePerTick * stacks, target.transform.position, source);
    }

    private void OnValidate()
    {
        if (TickInterval <= 0f)
            Debug.LogWarning($"{name}: Damage Over Time needs a Tick Interval above 0, otherwise it never deals damage.", this);
    }
}
