using UnityEngine;
 
public sealed class WeaponFaultDefect : Defect
{
    [Header("Weapon fault")]
    [Tooltip("What stops working on this half's weapons while the defect is active.")]
    [SerializeField] private WeaponFault _fault = WeaponFault.PitchLocked;
 
    private void Reset() => SetDefaults("Jammed Elevation", MinigameType.Mash, 10);
 
    protected override void OnActivated() => RefreshHalf();
    protected override void OnRepaired() => RefreshHalf();
 
    private void RefreshHalf()
    {
        if (!IsServer) return;
 
        Wagonhalf half = GetComponentInParent<Wagonhalf>();
        Transform root = half != null ? half.transform : transform.parent;
        if (root == null) return;
 
        WeaponFault faults = WeaponFault.None;
        foreach (WeaponFaultDefect defect in root.GetComponentsInChildren<WeaponFaultDefect>(true))
            if (defect.IsBroken) faults |= defect._fault;
 
        foreach (WeaponBase weapon in root.GetComponentsInChildren<WeaponBase>(true))
            weapon.SetFaultsServer(faults);
    }
}