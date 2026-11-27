using UnityEngine;

public struct StatusModifiers
{
    public float SpeedMultiplier;  
    public bool ReverseMovement;   
    public Color ScreenOverlay;  
    public float CameraSway;  
    public float VolumeMultiplier;

    public static StatusModifiers None => new StatusModifiers
    {
        SpeedMultiplier = 1f,
        ScreenOverlay = Color.clear,
        VolumeMultiplier = 1f
    };
}

public enum StatusStacking
{
    Refresh,   
    Stack,   
    Extend,   
    Ignore   
}

public abstract class StatusEffect : ScriptableObject
{
    [Tooltip("Name for UI and logs. Empty = the asset name.")]
    [SerializeField] private string _displayName;

    [Tooltip("Seconds. 0 = until removed by code, e.g. when a defect's Active Effect ends.")]
    [SerializeField, Min(0f)] private float _duration = 5f;

    [Tooltip("What happens when it's applied again while still active.")]
    [SerializeField] private StatusStacking _stacking = StatusStacking.Refresh;

    [Tooltip("Only used with Stacking = Stack.")]
    [SerializeField, Min(1)] private int _maxStacks = 1;

    [Tooltip("Seconds between ticks (server only), e.g. burn damage. 0 = no ticks.")]
    [SerializeField, Min(0f)] private float _tickInterval = 0f;

    public string DisplayName => string.IsNullOrWhiteSpace(_displayName) ? name : _displayName;
    public float Duration => _duration;
    public StatusStacking Stacking => _stacking;
    public int MaxStacks => _maxStacks;
    public float TickInterval => _tickInterval;

    public virtual void Modify(ref StatusModifiers mods, int stacks, float remaining) { }

    public virtual void Tick(StatusEffectReceiver target, int stacks, ulong source) { }

    protected static float FadeOut(float remaining, float seconds)
        => seconds > 0f ? Mathf.Clamp01(remaining / seconds) : 1f;
}
