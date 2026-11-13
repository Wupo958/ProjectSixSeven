using TMPro;
using UnityEngine;

public class ProfileSummaryUI : MonoBehaviour
{
    [SerializeField] private TMP_Text summaryText;
    [SerializeField] private RankTable rankTable;
    [SerializeField] private MedalRegistry medalRegistry;

    private void OnEnable()
    {
        ProfileService.ProfileChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        ProfileService.ProfileChanged -= Refresh;
    }

    private void Refresh()
    {
        if (!ProfileService.IsLoaded)
        {
            summaryText.text = "Not signed in";
            return;
        }

        PlayerProfile profile = ProfileService.Current;
        RankTier rank = rankTable.GetRank(profile.SuccessfulMissions);

        string summary = profile.DisplayName + "\n";
        summary += rank.DisplayName + " - " + profile.SuccessfulMissions + " successful missions\n";
        summary += "Medals: " + profile.UnlockedMedalIds.Count;

        foreach (string medalId in profile.UnlockedMedalIds)
        {
            MedalDefinition medal = medalRegistry.Find(medalId);
            if (medal != null)
            {
                summary += "\n  " + medal.DisplayName;
            }
        }

        summaryText.text = summary;
    }
}
