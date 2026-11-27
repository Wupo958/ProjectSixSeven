using UnityEngine;

//turns game audio down. E.g. Deafened. Players only. Voice chat is not affected.
[CreateAssetMenu(menuName = "Status Effects/Audio Muffle", fileName = "Deafened")]
public sealed class AudioMuffleEffect : StatusEffect
{
    [Tooltip("Game audio volume while active. 0 = silent, 1 = normal.")]
    [SerializeField, Range(0f, 1f)] private float _volume = 0.15f;

    [Tooltip("Seconds at the end over which hearing comes back.")]
    [SerializeField, Min(0f)] private float _fadeOut = 1.5f;

    public override void Modify(ref StatusModifiers mods, int stacks, float remaining)
    {
        float volume = Mathf.Lerp(1f, _volume, FadeOut(remaining, _fadeOut));
        mods.VolumeMultiplier = Mathf.Min(mods.VolumeMultiplier, volume);
    }
}
