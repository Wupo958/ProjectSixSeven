using UnityEngine;
 
public abstract class Defect : MinigameStation
{
    [Header("Visuals")]
    [Tooltip("Optional object shown only while the defect is active (smoke, sparks, ...).")]
    [SerializeField] private GameObject _activeVisual;
 
    [Tooltip("Renderers that switch to the broken material while active. " +
             "Empty = this object's own renderer.")]
    [SerializeField] private Renderer[] _renderers;
 
    [Tooltip("Material while active. Empty = the train's Broken Mat.")]
    [SerializeField] private Material _brokenMaterialOverride;
 
    private Material[][] _originalMaterials;
 
    protected TrainHealth Train { get; private set; }
    public int WagonIndex { get; private set; } = -1;
    public int PointIndex { get; private set; } = -1;
    public bool IsBroken => IsActive;
 
    // ----- wiring (called by TrainHealth) ----------------------------------
 
    public void Bind(TrainHealth train, int wagonIndex, int pointIndex)
    {
        Train = train;
        WagonIndex = wagonIndex;
        PointIndex = pointIndex;
 
        if (_renderers == null || _renderers.Length == 0)
            _renderers = GetComponents<Renderer>();
 
        if (_originalMaterials == null)
        {
            _originalMaterials = new Material[_renderers.Length][];
            for (int i = 0; i < _renderers.Length; i++)
                _originalMaterials[i] = _renderers[i] != null ? _renderers[i].sharedMaterials : null;
        }
    }
 
    public void SetBroken(bool broken)
    {
        ApplyVisual(broken);
        SetActiveState(broken);
    }
 
    // ----- station behaviour ---------------------------------------------------
 
    protected override bool IsAvailable => IsActive && Train != null;
    protected override string ActionVerb => "Repair";
 
    protected override void Complete(PlayerInteractor player) =>
        Train.RepairDefectServerRpc(WagonIndex, PointIndex);
 
    protected override void ReportFailure(PlayerInteractor player) =>
        Train.DefectFailedServerRpc(WagonIndex, PointIndex);
 
    // ----- hooks for child classes -------------------------------------------

    protected sealed override void OnCompleted() => OnRepaired();
    protected virtual void OnRepaired() { }
 
    public virtual int ModifyIncomingDamage(int damage) => damage;
 
    protected virtual void ApplyVisual(bool broken)
    {
        if (_activeVisual != null) _activeVisual.SetActive(broken);
        if (_renderers == null || _originalMaterials == null) return;
 
        Material brokenMat = _brokenMaterialOverride != null ? _brokenMaterialOverride : Train != null ? Train.BrokenMaterial : null;
 
        for (int i = 0; i < _renderers.Length; i++)
        {
            Renderer r = _renderers[i];
            Material[] original = _originalMaterials[i];
            if (r == null || original == null) continue;
 
            if (broken && brokenMat != null)
            {
                var mats = new Material[original.Length];
                for (int m = 0; m < mats.Length; m++) mats[m] = brokenMat;
                r.sharedMaterials = mats;
            }
            else
            {
                r.sharedMaterials = original;
            }
        }
    }
}