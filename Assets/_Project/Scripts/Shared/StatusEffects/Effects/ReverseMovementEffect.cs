using UnityEngine;

//movement input is reversed. E.g. Disoriented. Players only.
[CreateAssetMenu(menuName = "Status Effects/Reverse Movement", fileName = "Disoriented")]
public sealed class ReverseMovementEffect : StatusEffect
{
    public override void Modify(ref StatusModifiers mods, int stacks, float remaining)
        => mods.ReverseMovement = true;
}
