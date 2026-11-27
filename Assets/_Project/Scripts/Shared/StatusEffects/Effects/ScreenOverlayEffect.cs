using UnityEngine;

//covers the screen with a colour. E.g. Blinded. Players only, needs StatusEffectScreen on the Canvas.
[CreateAssetMenu(menuName = "Status Effects/Screen Overlay", fileName = "Blinded")]
public sealed class ScreenOverlayEffect : StatusEffect
{
    [Tooltip("Colour over the screen. Alpha = how much you can't see (1 = nothing).")]
    [SerializeField] private Color _color = new Color(1f, 1f, 1f, 0.95f);

    [Tooltip("Seconds at the end over which the overlay fades away.")]
    [SerializeField, Min(0f)] private float _fadeOut = 1.5f;

    public override void Modify(ref StatusModifiers mods, int stacks, float remaining)
    {
        Color color = _color;
        color.a *= FadeOut(remaining, _fadeOut);
        if (color.a > mods.ScreenOverlay.a) mods.ScreenOverlay = color; // strongest wins
    }
}
