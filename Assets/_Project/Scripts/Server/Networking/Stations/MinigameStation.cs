using Unity.Netcode;
using UnityEngine;
 
public abstract class MinigameStation : Interactable
{
    [Header("Station")]
    [SerializeField] private string _displayName = "Station";
 
    [Tooltip("Item the player must hold to start. Empty = none needed. " +
             "Whether it is used up is set on the item (Consume On Use).")]
    [SerializeField] private ItemDefinition _requiredItem;
 
    [SerializeField] private MinigameType _minigame = MinigameType.Mash;
 
    [Tooltip("Meaning depends on the minigame. Mash: number of presses. Hold: seconds.")]
    [SerializeField] private int _difficulty = 10;
 
    [Tooltip("Only one player at a time. Others see \"in use\" until they're done.")]
    [SerializeField] private bool _exclusive = true;
 
    private StationHub _hub;
    private int _stationIndex = -1;
    private bool _waitingForLock;
    private bool _holdingLock;
 
    public string DisplayName => _displayName;
    public ItemDefinition RequiredItem => _requiredItem;
    public MinigameType Minigame => _minigame;
    public int Difficulty => _difficulty;
    public bool IsActive { get; private set; }
 
    protected static bool IsServer => NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;
 
    protected void SetDefaults(string displayName, MinigameType minigame, int difficulty)
    {
        _displayName = displayName;
        _minigame = minigame;
        _difficulty = difficulty;
    }
 
    public void BindStation(StationHub hub, int index)
    {
        _hub = hub;
        _stationIndex = index;
    }

    protected StationHub Hub => _hub;
    protected int StationIndex => _stationIndex;

    public virtual void ApplyStationState(double activeSince) { }
 
    // ----- decided by the child class -----------------------------------------
 
    protected virtual bool IsAvailable => IsActive;
 
    protected virtual string ActionVerb => "Use";
 
    protected virtual string IdlePrompt(PlayerInteractor player) => string.Empty;
 
    protected abstract void Complete(PlayerInteractor player);
 
    protected virtual void ReportFailure(PlayerInteractor player)
    {
        if (_hub != null && _stationIndex >= 0) _hub.FailedServerRpc(_stationIndex);
    }
 
    // ----- effect hooks ----------------------------------------------------------
 
    protected virtual void OnActivated() { }
 
    protected virtual void WhileActive(float deltaTime) { }
 
    protected virtual void OnCompleted() { }
 
    protected virtual void OnFailedLocal(PlayerInteractor player) { }
 
    public virtual void OnFailedServer() { }
 
    protected void SetActiveState(bool active)
    {
        if (active == IsActive) return;
        IsActive = active;
        if (active) OnActivated();
        else OnCompleted();
    }
 
    protected virtual void Update()
    {
        if (IsActive) WhileActive(Time.deltaTime);
 
        if (_holdingLock && (MinigameHost.Instance == null || !MinigameHost.Instance.IsRunning))
            ReleaseLock();
    }
 
    // ----- interaction -------------------------------------------------------------
 
    private bool Lockable => _exclusive && _hub != null && _hub.IsSpawned && _stationIndex >= 0;
 
    public bool IsLockedByOther
    {
        get
        {
            if (!Lockable) return false;
            ulong holder = _hub.HolderOf(_stationIndex);
            return holder != StationHub.Free && holder != NetworkManager.Singleton.LocalClientId;
        }
    }
 
    protected bool HasRequiredItem(PlayerInteractor player)
    {
        if (_requiredItem == null) return true;
        return player.Inventory != null && player.Inventory.Held == _requiredItem;
    }
 
    public override bool CanInteract(PlayerInteractor player)
    {
        return IsAvailable && !IsLockedByOther && !_waitingForLock && HasRequiredItem(player) &&
               MinigameHost.Instance != null && !MinigameHost.Instance.IsRunning;
    }
 
    public override string GetPrompt(PlayerInteractor player)
    {
        if (!IsAvailable) return IdlePrompt(player);
        if (IsLockedByOther) return $"{_displayName} - in use";
        if (!HasRequiredItem(player)) return $"{_displayName} - requires {_requiredItem.DisplayName}";
        return $"[E] {ActionVerb} {_displayName}";
    }
 
    public override void Interact(PlayerInteractor player)
    {
        if (!CanInteract(player)) return;
 
        if (!Lockable)
        {
            StartMinigame(player);
            return;
        }
 
        _waitingForLock = true;
        _hub.RequestLock(_stationIndex, granted =>
        {
            _waitingForLock = false;
            if (!granted) return;
 
            _holdingLock = true;
            if (IsAvailable && MinigameHost.Instance != null && !MinigameHost.Instance.IsRunning)
                StartMinigame(player);
        });
    }
 
    private void StartMinigame(PlayerInteractor player)
    {
        MinigameHost.Instance.Run(_minigame, _difficulty, player,
            onSuccess: () =>
            {
                if (!IsAvailable || !gameObject.activeInHierarchy) return;
                Complete(player);
                if (_requiredItem != null) player.Inventory?.NotifyUsed();
            },
            onFail: () =>
            {
                OnFailedLocal(player);
                ReportFailure(player);
            });
    }
 
    private void ReleaseLock()
    {
        _holdingLock = false;
        if (Lockable) _hub.Release(_stationIndex);
    }
}