using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class MedalDebugUnlocker : MonoBehaviour
{
    [SerializeField] private MedalDefinition testMedal;

    private void Update()
    {
        if (!ProfileService.IsLoaded)
        {
            return;
        }

        if (Keyboard.current.f8Key.wasPressedThisFrame)
        {
            MedalAwards.AwardAll(testMedal);
        }

        if (Keyboard.current.f9Key.wasPressedThisFrame)
        {
            MedalAwards.Award(testMedal, NetworkManager.Singleton.LocalClientId);
        }

        if (Keyboard.current.f10Key.wasPressedThisFrame)
        {
            _ = ProfileService.AddSuccessfulMission();
        }
    }
}
