using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class PlayerMedalDisplay : NetworkBehaviour
{
    [SerializeField] private List<MedalSlot> medalSlots = new List<MedalSlot>();

    private NetworkList<FixedString32Bytes> unlockedMedalIds = new NetworkList<FixedString32Bytes>(
        null,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);

    public override void OnNetworkSpawn()
    {
        unlockedMedalIds.OnListChanged += OnMedalsChanged;

        if (IsOwner)
        {
            foreach (string medalId in ProfileService.Current.UnlockedMedalIds)
            {
                unlockedMedalIds.Add(new FixedString32Bytes(medalId));
            }

            ProfileService.MedalUnlocked += OnLocalMedalUnlocked;
        }

        RefreshVisuals();
    }

    public override void OnNetworkDespawn()
    {
        unlockedMedalIds.OnListChanged -= OnMedalsChanged;

        if (IsOwner)
        {
            ProfileService.MedalUnlocked -= OnLocalMedalUnlocked;
        }
    }

    private void OnLocalMedalUnlocked(string medalId)
    {
        unlockedMedalIds.Add(new FixedString32Bytes(medalId));
    }

    private void OnMedalsChanged(NetworkListEvent<FixedString32Bytes> change)
    {
        RefreshVisuals();
    }

    private void RefreshVisuals()
    {
        foreach (MedalSlot slot in medalSlots)
        {
            slot.Visual.SetActive(HasMedal(slot.Medal.Id));
        }
    }

    private bool HasMedal(string medalId)
    {
        foreach (FixedString32Bytes unlockedId in unlockedMedalIds)
        {
            if (unlockedId.ToString() == medalId)
            {
                return true;
            }
        }

        return false;
    }
}
