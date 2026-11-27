using UnityEngine;

public sealed class LooseBoltsTask : MaintenanceTask
{
    [Header("Loose Bolts")]
    [Tooltip("Seconds between damage ticks.")]
    [SerializeField, Min(0.5f)] private float _tickInterval = 5f;
 
    [Tooltip("Damage per tick at full strength. Starts near 0 when the task becomes due.")]
    [SerializeField, Min(0)] private int _maxDamagePerTick = 50;
 
    private Collider _collider;
    private float _nextTick;
 
    private void Reset() => SetDefaults("Loose Bolts", MinigameType.Mash, 12);
 
    protected override void Awake()
    {
        base.Awake();
        _collider = GetComponentInChildren<Collider>();
    }
 
    protected override string ActionVerb => "Tighten";
 
    protected override void OnActivated() => _nextTick = Time.time + _tickInterval;
 
    protected override void WhileActive(float deltaTime)
    {
        Wagon wagon = GetComponentInParent<Wagon>();
        if (wagon != null && wagon.IsDestroyed) return;

        if (!IsServer || Time.time < _nextTick || TrainHealth.Instance == null) return;
        _nextTick = Time.time + _tickInterval;
 
        int damage = Mathf.RoundToInt(_maxDamagePerTick * Severity);
        if (damage > 0) TrainHealth.Instance.ReportHit(_collider, transform.position, damage);
    }
}