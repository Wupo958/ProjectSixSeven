using UnityEngine;

//changes movement speed. E.g. Slowed. Works on enemies and players.
[CreateAssetMenu(menuName = "Status Effects/Speed", fileName = "Slowed")]
public sealed class SpeedEffect : StatusEffect
{
    [Tooltip("Speed multiplier per stack. 0.5 = half speed, 2 stacks = quarter speed.")]
    [SerializeField, Range(0f, 2f)] private float _multiplier = 0.5f;

    public override void Modify(ref StatusModifiers mods, int stacks, float remaining)
        => mods.SpeedMultiplier *= Mathf.Pow(_multiplier, stacks);
}
