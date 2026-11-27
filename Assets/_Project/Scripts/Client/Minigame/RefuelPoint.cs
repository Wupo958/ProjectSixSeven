using UnityEngine;
 
public sealed class RefuelPoint : Interactable
{
    [SerializeField] private MinigameType _minigame = MinigameType.Refuel;
 
    [Tooltip("Meaning depends on the minigame. For Hold: seconds to hold.")]
    [SerializeField, Min(1)] private int _difficulty = 3;
 
    private static LokFuel Tank => LokFuel.Instance;
 
    private static bool HoldsFuel(PlayerInteractor player)
    {
        ItemDefinition held = player.Inventory != null ? player.Inventory.Held : null;
        return held != null && held.Type == ItemType.Fuel;
    }
 
    public override bool CanInteract(PlayerInteractor player)
    {
        return Tank != null && !Tank.IsFull && HoldsFuel(player) &&
               MinigameHost.Instance != null && !MinigameHost.Instance.IsRunning;
    }
 
    public override string GetPrompt(PlayerInteractor player)
    {
        if (Tank == null) return string.Empty;
 
        int percent = Mathf.RoundToInt(Tank.Fraction * 100f);
        if (Tank.IsFull) return $"Fuel tank full ({percent}%)";
        if (!HoldsFuel(player)) return $"Fuel tank {percent}% - bring Fuel";
        return $"[E] Refuel ({percent}%)";
    }
 
    public override void Interact(PlayerInteractor player)
    {
        if (MinigameHost.Instance == null) return;
 
        MinigameHost.Instance.Run(_minigame, _difficulty, player,
            onSuccess: () =>
            {
                if (Tank == null || !HoldsFuel(player)) return;
                Tank.RequestRefuel();
                player.Inventory.NotifyUsed();
            });
    }
}