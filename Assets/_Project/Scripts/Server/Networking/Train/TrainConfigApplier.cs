using UnityEngine;
 
[RequireComponent(typeof(Trainbuilder))]
public sealed class TrainConfigApplier : MonoBehaviour
{
    private Trainbuilder _builder;
    private TrainConfig _config;
    private bool _rebuildQueued;
 
    private void Awake()
    {
        _builder = GetComponent<Trainbuilder>();
    }
 
    private void Start()
    {
        if (TrainConfig.Instance == null) _builder.Rebuild();
    }
 
    private void OnDestroy()
    {
        if (_config != null) _config.LayoutChanged -= QueueRebuild;
    }
 
    private void Update()
    {
        if (_config == null && TrainConfig.Instance != null)
        {
            _config = TrainConfig.Instance;
            _config.LayoutChanged += QueueRebuild;
            QueueRebuild();
        }
    }
 
    private void QueueRebuild() => _rebuildQueued = true;
 
    private void LateUpdate()
    {
        if (!_rebuildQueued || _config == null) return;
        _rebuildQueued = false;
        _builder.RebuildFrom(_config.Snapshot(), _config.Registry);
    }
}
