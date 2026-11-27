using Unity.Netcode;
using UnityEngine;

public abstract class MaintenanceTask : MinigameStation
{
    [Header("Maintenance")]
    [Tooltip("Seconds until the task is due, picked randomly between Min and Max " +
             "each time (at the start and after every completion).")]
    [SerializeField, Min(0f)] private float _minInterval = 60f;
    [SerializeField, Min(0f)] private float _maxInterval = 120f;
 
    [Tooltip("Seconds after becoming due until the active effect is at full strength.")]
    [SerializeField, Min(0.1f)] private float _rampSeconds = 60f;
 
    [Tooltip("Optional object shown only while the task is due (dripping oil, rattling plate, ...).")]
    [SerializeField] private GameObject _activeVisual;
 
    private double _activeSince = -1.0;
    private double _nextDue = -1.0;
 
    protected static double Now => NetworkManager.Singleton != null ? NetworkManager.Singleton.ServerTime.Time : Time.timeAsDouble;
 
    public float Severity => IsActive && _activeSince >= 0.0 ? Mathf.Clamp01((float)((Now - _activeSince) / _rampSeconds)) : 0f;
 
    protected override bool IsAvailable => IsActive && Hub != null;
    protected override string ActionVerb => "Fix";
 
    protected virtual void Awake()
    {
        if (_activeVisual != null) _activeVisual.SetActive(false);
    }
 
    public override void ApplyStationState(double activeSince)
    {
        _activeSince = activeSince;
        bool active = activeSince >= 0.0;
 
        if (_activeVisual != null) _activeVisual.SetActive(active);
        if (!active && IsServer) ScheduleNext();
 
        SetActiveState(active);
    }
 
    public override string GetPrompt(PlayerInteractor player)
    {
        string prompt = base.GetPrompt(player);
        if (!IsActive || string.IsNullOrEmpty(prompt)) return prompt;
        return $"{prompt}  ({Mathf.RoundToInt(Severity * 100f)}%)";
    }
 
    protected override void Update()
    {
        base.Update();
 
        if (!IsServer || IsActive || Hub == null || !Hub.IsSpawned || StationIndex < 0) return;
 
        if (_nextDue < 0.0)
        {
            ScheduleNext();
        }
        else if (Now >= _nextDue)
        {
            _nextDue = -1.0;
            Hub.SetActive(StationIndex, true);
        }
    }
 
    private void ScheduleNext() =>
        _nextDue = Now + Random.Range(_minInterval, Mathf.Max(_minInterval, _maxInterval));
 
    protected override void Complete(PlayerInteractor player) => Hub.DeactivateServerRpc(StationIndex);
 
    // ----- testing (MaintenanceTaskEditor) -------------------------------------------
 
    public void DebugSetActive(bool active)
    {
        if (Hub != null && Hub.IsSpawned && StationIndex >= 0) Hub.DebugSetActiveServerRpc(StationIndex, active);
    }
}