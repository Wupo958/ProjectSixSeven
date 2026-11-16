using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class RouteOptionCard : MonoBehaviour
{
    [SerializeField] private TMP_Text _titleLabel;
    [SerializeField] private TMP_Text _lengthLabel;
    [SerializeField] private TMP_Text _wavesLabel;
    [SerializeField] private TMP_Text _enemiesLabel;
    [SerializeField] private TMP_Text _payoutLabel;
    [SerializeField] private TMP_Text _votersLabel;
    [SerializeField] private Button _voteButton;

    [Tooltip("Optional. Shown while the local player has voted for this route.")]
    [SerializeField] private GameObject _ownVoteMarker;

    [Tooltip("Optional. Shown while this route has an absolute majority.")]
    [SerializeField] private GameObject _majorityMarker;

    private RouteMapUI _owner;
    private int _index;

    public void Init(RouteMapUI owner, int index)
    {
        _owner = owner;
        _index = index;

        if (_voteButton != null)
        {
            _voteButton.onClick.AddListener(OnVoteClicked);
        }
    }

    public void Show(RouteOption route, EnemyRegistry registry, List<string> voters, int requiredVotes, bool ownVote, bool majority)
    {
        SetText(_titleLabel, "Route " + (_index + 1));
        SetText(_lengthLabel, (route.Length / 1000f).ToString("0.0") + " km");
        SetText(_wavesLabel, FormatWaves(route));
        SetText(_enemiesLabel, FormatEnemies(route, registry));
        SetText(_payoutLabel, "Payout: " + route.Payout);
        SetText(_votersLabel, FormatVoters(voters, requiredVotes));

        if (_ownVoteMarker != null)
        {
            _ownVoteMarker.SetActive(ownVote);
        }

        if (_majorityMarker != null)
        {
            _majorityMarker.SetActive(majority);
        }
    }

    private void OnVoteClicked()
    {
        if (_owner != null)
        {
            _owner.OnVoteClicked(_index);
        }
    }

    private static string FormatWaves(RouteOption route)
    {
        string groups = " groups";
        if (route.WaveSize == 1)
        {
            groups = " group";
        }

        return "Wave every " + route.WaveInterval.ToString("0") + " s, " + route.WaveSize + groups;
    }

    private static string FormatEnemies(RouteOption route, EnemyRegistry registry)
    {
        List<int> ids = new List<int>();
        List<int> counts = new List<int>();

        for (int i = 0; i < route.EnemyPool.Length; i++)
        {
            int id = route.EnemyPool[i];
            int known = ids.IndexOf(id);
            if (known >= 0)
            {
                counts[known]++;
            }
            else
            {
                ids.Add(id);
                counts.Add(1);
            }
        }

        StringBuilder text = new StringBuilder("Enemies: ");
        for (int i = 0; i < ids.Count; i++)
        {
            if (i > 0)
            {
                text.Append(", ");
            }

            text.Append(EnemyName(registry, ids[i]));
            text.Append(" x");
            text.Append(counts[i]);
        }

        return text.ToString();
    }

    private static string EnemyName(EnemyRegistry registry, int id)
    {
        if (registry == null)
        {
            return "?";
        }

        EnemyBase enemy = registry.Get(id);
        if (enemy == null)
        {
            return "?";
        }

        return enemy.DisplayName;
    }

    private static string FormatVoters(List<string> voters, int requiredVotes)
    {
        string text = "Votes " + voters.Count + "/" + requiredVotes;
        if (voters.Count > 0)
        {
            text += ": " + string.Join(", ", voters);
        }

        return text;
    }

    private static void SetText(TMP_Text label, string text)
    {
        if (label != null)
        {
            label.text = text;
        }
    }
}
