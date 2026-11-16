using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public sealed class RouteMapUI : MonoBehaviour
{
    [Tooltip("Root of the map. Shown while route options exist.")]
    [SerializeField] private GameObject _panel;

    [Tooltip("One card per route option, in order.")]
    [SerializeField] private RouteOptionCard[] _cards;

    [Tooltip("Appears once one route has an absolute majority. Every player may press it.")]
    [SerializeField] private Button _startButton;

    [SerializeField] private TMP_Text _statusLabel;

    private readonly List<string> _voters = new List<string>();
    private GameSession _session;

    private void Awake()
    {
        if (_panel != null)
        {
            _panel.SetActive(false);
        }

        for (int i = 0; i < _cards.Length; i++)
        {
            _cards[i].Init(this, i);
        }

        if (_startButton != null)
        {
            _startButton.onClick.AddListener(OnStartClicked);
            _startButton.gameObject.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (_session != null)
        {
            _session.RoutesChanged -= Refresh;
        }
    }

    private void Update()
    {
        if (_session == null && GameSession.Instance != null)
        {
            _session = GameSession.Instance;
            _session.RoutesChanged += Refresh;
            Refresh();
        }
    }

    public void OnVoteClicked(int optionIndex)
    {
        if (_session != null)
        {
            _session.CastVote(optionIndex);
        }
    }

    private void OnStartClicked()
    {
        if (_session != null)
        {
            _session.RequestStart();
        }
    }

    private void Refresh()
    {
        if (_session == null)
        {
            return;
        }

        int optionCount = _session.RouteOptionCount;
        if (_panel != null)
        {
            _panel.SetActive(optionCount > 0);
        }

        int majority = _session.MajorityOption;
        ulong localClientId = LocalClientId();

        for (int i = 0; i < _cards.Length; i++)
        {
            bool hasOption = i < optionCount;
            _cards[i].gameObject.SetActive(hasOption);
            if (!hasOption)
            {
                continue;
            }

            bool ownVote = CollectVoters(i, localClientId);
            _cards[i].Show(_session.GetRouteOption(i), _session.EnemyRegistry, _voters, _session.RequiredVotes, ownVote, majority == i);
        }

        bool hasMajority = majority != GameSession.NoMajority;
        if (_startButton != null)
        {
            _startButton.gameObject.SetActive(hasMajority);
        }

        if (_statusLabel != null)
        {
            if (hasMajority)
            {
                _statusLabel.text = "Route " + (majority + 1) + " chosen";
            }
            else
            {
                _statusLabel.text = "Vote for the next route (" + _session.RequiredVotes + " votes needed)";
            }
        }
    }

    private bool CollectVoters(int optionIndex, ulong localClientId)
    {
        _voters.Clear();
        bool ownVote = false;

        for (int i = 0; i < _session.VoteCount; i++)
        {
            RouteVote vote = _session.GetVote(i);
            if (vote.Option != optionIndex)
            {
                continue;
            }

            _voters.Add(vote.PlayerName.ToString());
            if (vote.ClientId == localClientId)
            {
                ownVote = true;
            }
        }

        return ownVote;
    }

    private static ulong LocalClientId()
    {
        if (NetworkManager.Singleton == null)
        {
            return ulong.MaxValue;
        }

        return NetworkManager.Singleton.LocalClientId;
    }
}
