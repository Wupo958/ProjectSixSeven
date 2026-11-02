using TMPro;
using UnityEngine;

public class LevelStatsPanel : MonoBehaviour
{
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text statsText;
    [SerializeField] private string wonTitleFormat = "Level {0} completed";
    [SerializeField] private string lostTitleFormat = "Game stats";

    private GameSession session;

    private void Start()
    {
        session = GameSession.Instance;
        if (session != null)
        {
            session.StatsChanged += Refresh;
        }

        Refresh();
    }

    private void OnDestroy()
    {
        if (session != null)
        {
            session.StatsChanged -= Refresh;
        }
    }

    private void Refresh()
    {
        if (session == null)
        {
            return;
        }

        bool won = session.LastLevelWon;

        if (titleText != null)
        {
            titleText.text = string.Format(won ? wonTitleFormat : lostTitleFormat, session.LevelIndex + 1);
        }

        if (statsText == null)
        {
            return;
        }

        if (won)
        {
            statsText.text = FormatStats(session.Current);
        }
        else
        {
            statsText.text = $"Levels completed: {session.LevelIndex}\n" + FormatStats(session.Total);
        }
    }

    private static string FormatStats(LevelStats stats)
    {
        return
            $"Kills: {stats.Kills}\n" +
            $"Damage taken: {stats.DamageTaken}\n" +
            $"Repairs: {stats.Repairs}\n" +
            $"Time: {FormatTime(stats.Time)}";
    }

    private static string FormatTime(float seconds)
    {
        int minutes = Mathf.FloorToInt(seconds / 60f);
        int rest = Mathf.FloorToInt(seconds % 60f);
        return $"{minutes:00}:{rest:00}";
    }
}
