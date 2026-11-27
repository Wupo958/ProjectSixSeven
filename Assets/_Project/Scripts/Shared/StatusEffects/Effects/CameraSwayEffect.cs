using UnityEngine;

//the camera sways around. E.g. Dazed. Players only.
[CreateAssetMenu(menuName = "Status Effects/Camera Sway", fileName = "Dazed")]
public sealed class CameraSwayEffect : StatusEffect
{
    [Tooltip("How far the camera sways, in degrees.")]
    [SerializeField, Min(0f)] private float _degrees = 6f;

    [Tooltip("Seconds at the end over which the sway calms down.")]
    [SerializeField, Min(0f)] private float _fadeOut = 1f;

    public override void Modify(ref StatusModifiers mods, int stacks, float remaining)
        => mods.CameraSway = Mathf.Max(mods.CameraSway, _degrees * FadeOut(remaining, _fadeOut));
}
