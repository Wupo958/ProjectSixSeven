using UnityEngine;
 
[DisallowMultipleComponent]
public sealed class RepairPoint : Interactable
{
    private TrainHealth _train;
    private int _wagon;
    private int _point;

    private bool isRepairPending;
    private float repairPendingUntil;
    private float pendingTimeout = 3f;

    public void Setup(TrainHealth train, int wagonIndex, int pointIndex)
    {
        if (_train != null)
        {
            _train.OnWagonChanged -= OnWagonChanged;
        }

        _train = train;
        _wagon = wagonIndex;
        _point = pointIndex;
        isRepairPending = false;

        _train.OnWagonChanged += OnWagonChanged;
    }

    private void OnDestroy()
    {
        if (_train != null)
        {
            _train.OnWagonChanged -= OnWagonChanged;
        }
    }

    private void OnWagonChanged(int wagonIndex)
    {
        if (wagonIndex == _wagon)
        {
            isRepairPending = false;
        }
    }

    private bool IsRepairPending()
    {
        if (!isRepairPending)
        {
            return false;
        }

        if (Time.time > repairPendingUntil)
        {
            isRepairPending = false;
            return false;
        }

        return true;
    }
 
    public bool IsBroken => _train != null && _train.IsDefectBroken(_wagon, _point);
 
    public override bool CanInteract(PlayerInteractor player)
    {
        if (IsRepairPending()) return false;
        if (!IsBroken) return false;
        ItemDefinition held = player.Inventory != null ? player.Inventory.Held : null;
        return held != null && held.CanRepair;
    }
 
    public override string GetPrompt(PlayerInteractor player)
    {
        if (IsRepairPending()) return string.Empty;
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
                isRepairPending = true;
                repairPendingUntil = Time.time + pendingTimeout;
                _train.RepairDefectServerRpc(_wagon, _point);
                player.Inventory?.NotifyUsed();
            });
    }
}
