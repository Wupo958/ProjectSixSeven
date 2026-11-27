using UnityEngine;
 
public sealed class WeaponMaintenancePoint : Interactable
{
    [Tooltip("Empty = the weapon this object sits under.")]
    [SerializeField] private WeaponBase _weapon;
 
    [Tooltip("Item needed, e.g. an oil can (type Tool, so it's kept). Empty = none needed.")]
    [SerializeField] private ItemDefinition _requiredItem;
 
    [SerializeField] private MinigameType _minigame = MinigameType.Maintenance;
 
    [Tooltip("Meaning depends on the minigame. For Hold: seconds to hold.")]
    [SerializeField, Min(1)] private int _difficulty = 3;
 
    [Tooltip("Verb in the prompt: \"[E] Oil Cannon\".")]
    [SerializeField] private string _action = "Oil";
 
    [Tooltip("Below this wear the turret doesn't need maintenance yet.")]
    [SerializeField, Range(0f, 1f)] private float _minWear = 0.05f;
 
    private void Awake()
    {
        if (_weapon == null) _weapon = GetComponentInParent<WeaponBase>();
    }
 
    private string WeaponName =>
        _weapon.Definition != null ? _weapon.Definition.DisplayName : "turret";
 
    private bool HasItem(PlayerInteractor player) =>
        _requiredItem == null || (player.Inventory != null && player.Inventory.Held == _requiredItem);
 
    public override bool CanInteract(PlayerInteractor player) =>
        _weapon != null && _weapon.IsSpawned && _weapon.Wear >= _minWear && HasItem(player) &&
        MinigameHost.Instance != null && !MinigameHost.Instance.IsRunning;
 
    public override string GetPrompt(PlayerInteractor player)
    {
        if (_weapon == null || !_weapon.IsSpawned) return string.Empty;
        if (_weapon.Wear < _minWear) return $"{WeaponName} - in good shape";
 
        string state = _weapon.IsJammed ? "FAILED" : $"wear {Mathf.RoundToInt(_weapon.Wear * 100f)}%";
        if (!HasItem(player)) return $"{WeaponName} ({state}) - requires {_requiredItem.DisplayName}";
        return $"[E] {_action} {WeaponName} ({state})";
    }
 
    public override void Interact(PlayerInteractor player)
    {
        if (MinigameHost.Instance == null) return;
 
        MinigameHost.Instance.Run(_minigame, _difficulty, player,
            onSuccess: () =>
            {
                if (_weapon == null || !HasItem(player)) return;
                _weapon.RequestMaintenance();
                if (_requiredItem != null) player.Inventory?.NotifyUsed();
            });
    }
}