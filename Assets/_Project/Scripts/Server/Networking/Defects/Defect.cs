using Unity.Netcode;
using UnityEngine;
 
public abstract class Defect : Interactable
{
    [Header("Defect")]
    [SerializeField] private string _displayName = "Defect";
 
    [Tooltip("Item the player must hold to start the repair. Leave empty if none is needed. " +
             "Whether it is used up is set on the item (Consume On Use).")]
    [SerializeField] private ItemDefinition _requiredItem;
 
    [SerializeField] private MinigameType _minigame = MinigameType.Mash;
 
    [Tooltip("Meaning depends on the minigame. For Mash: number of presses.")]
    [SerializeField] private int _difficulty = 10;
 
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
    public bool IsBroken { get; private set; }
 
    public string DisplayName => _displayName;
    public ItemDefinition RequiredItem => _requiredItem;
 
    protected static bool IsServer =>
        NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;
 
    protected void SetDefaults(string displayName, MinigameType minigame, int difficulty)
    {
        _displayName = displayName;
        _minigame = minigame;
        _difficulty = difficulty;
    }
 
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
        bool changed = broken != IsBroken;
        IsBroken = broken;
        ApplyVisual(broken);
 
        if (!changed) return;
        if (broken) OnActivated();
        else OnRepaired();
    }
 
    protected virtual void Update()
    {
        if (IsBroken) WhileActive(Time.deltaTime);
    }
 
    // ----- hooks for child classes -------------------------------------------
 
    protected virtual void OnActivated() { }
 
    protected virtual void WhileActive(float deltaTime) { }
 
    protected virtual void OnRepaired() { }
 
    protected virtual void OnFailedLocal(PlayerInteractor player) { }
 
    public virtual void OnFailedServer() { }
 
    public virtual int ModifyIncomingDamage(int damage) => damage;
 
    protected virtual void ApplyVisual(bool broken)
    {
        if (_activeVisual != null) _activeVisual.SetActive(broken);
        if (_renderers == null || _originalMaterials == null) return;
 
        Material brokenMat = _brokenMaterialOverride != null ? _brokenMaterialOverride
                           : Train != null ? Train.BrokenMaterial : null;
 
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
 
    // ----- interaction ---------------------------------------------------------
 
    private bool HasRequiredItem(PlayerInteractor player)
    {
        if (_requiredItem == null) return true;
        return player.Inventory != null && player.Inventory.Held == _requiredItem;
    }
 
    public override bool CanInteract(PlayerInteractor player)
    {
        return IsBroken && Train != null && HasRequiredItem(player) &&
               MinigameHost.Instance != null && !MinigameHost.Instance.IsRunning;
    }
 
    public override string GetPrompt(PlayerInteractor player)
    {
        if (!IsBroken) return string.Empty;
        if (!HasRequiredItem(player)) return $"{_displayName} - requires {_requiredItem.DisplayName}";
        return $"[E] Repair {_displayName}";
    }
 
    public override void Interact(PlayerInteractor player)
    {
        if (MinigameHost.Instance == null) return;
 
        MinigameHost.Instance.Run(_minigame, _difficulty, player,
            onSuccess: () =>
            {
                // Someone else may have fixed it while we were busy; don't eat the item then.
                if (!IsBroken || !gameObject.activeInHierarchy) return;
                Train.RepairDefectServerRpc(WagonIndex, PointIndex);
                if (_requiredItem != null) player.Inventory?.NotifyUsed();
            },
            onFail: () =>
            {
                OnFailedLocal(player);
                Train.DefectFailedServerRpc(WagonIndex, PointIndex);
            });
    }
}
