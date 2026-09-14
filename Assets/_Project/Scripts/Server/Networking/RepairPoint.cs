using UnityEngine;
 
[DisallowMultipleComponent]
public sealed class RepairPoint : Interactable
{
    private TrainHealth _train;
    private int _wagon;
    private int _point;
 
    public void Setup(TrainHealth train, int wagonIndex, int pointIndex)
    {
        _train = train;
        _wagon = wagonIndex;
        _point = pointIndex;
    }
 
    public bool IsBroken => _train != null && _train.IsDefectBroken(_wagon, _point);
 
    public override bool CanInteract(PlayerInteractor player)
    {
        if (!IsBroken) return false;
        ItemDefinition held = player.Inventory != null ? player.Inventory.Held : null;
        return held != null && held.CanRepair;
    }
 
    public override string GetPrompt(PlayerInteractor player)
    {
        if (!IsBroken) return string.Empty;
 
        ItemDefinition held = player.Inventory != null ? player.Inventory.Held : null;
        if (held == null || !held.CanRepair) return "Broken - you need a hammer";
 
        return "[E] Repair";
    }
 
    public override void Interact(PlayerInteractor player)
    {
        if (_train == null || MinigameHost.Instance == null) return;
 
        MinigameHost.Instance.Run(
            _train.RepairMinigame,
            _train.RepairDifficulty,
            player,
            () =>
            {
                _train.RepairDefectServerRpc(_wagon, _point);
                player.Inventory?.NotifyUsed();
            });
    }
}
