using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class StationUI : MonoBehaviour
{
    private enum View
    {
        Lost,
        Rewards,
        TrainBuilder,
        RouteMap
    }

    [Header("Rewards window")]
    [Tooltip("Shown in the rewards window and on the game over screen (result text, stats).")]
    [SerializeField] private GameObject[] _resultObjects;

    [Tooltip("Payout and continue button. Only shown in the rewards window after a win.")]
    [SerializeField] private GameObject _rewardsFooter;

    [SerializeField] private TMP_Text _payoutLabel;
    [SerializeField] private Button _rewardsContinueButton;

    [Header("Station menus")]
    [SerializeField] private GameObject _trainBuilderView;
    [SerializeField] private GameObject _routeMapView;

    [Header("Switching")]
    [SerializeField] private GameObject _tabBar;
    [SerializeField] private Button _trainBuilderTab;
    [SerializeField] private Button _routeMapTab;
    [SerializeField] private Color _activeTabColor = new Color(0.553f, 0.698f, 0.333f);
    [SerializeField] private Color _inactiveTabColor = new Color(0.25f, 0.29f, 0.2f);

    private GameSession _session;
    private bool _initialized;

    private void Awake()
    {
        if (_rewardsContinueButton != null)
        {
            _rewardsContinueButton.onClick.AddListener(OnRewardsContinue);
        }

        if (_trainBuilderTab != null)
        {
            _trainBuilderTab.onClick.AddListener(OnTrainBuilderTab);
        }

        if (_routeMapTab != null)
        {
            _routeMapTab.onClick.AddListener(OnRouteMapTab);
        }
    }

    private void Update()
    {
        if (_initialized || GameSession.Instance == null)
        {
            return;
        }

        _session = GameSession.Instance;
        _initialized = true;

        if (_session.LastLevelWon)
        {
            ShowRewards();
        }
        else
        {
            Show(View.Lost);
        }
    }

    private void ShowRewards()
    {
        if (_payoutLabel != null)
        {
            _payoutLabel.text = "Resources earned: " + _session.CurrentRoute.Payout
                + "   (total " + _session.TeamResources + ")";
        }

        Show(View.Rewards);
    }

    private void OnRewardsContinue()
    {
        Show(View.TrainBuilder);
    }

    private void OnTrainBuilderTab()
    {
        Show(View.TrainBuilder);
    }

    private void OnRouteMapTab()
    {
        Show(View.RouteMap);
    }

    private void Show(View view)
    {
        bool showResult = view == View.Lost || view == View.Rewards;
        for (int i = 0; i < _resultObjects.Length; i++)
        {
            if (_resultObjects[i] != null)
            {
                _resultObjects[i].SetActive(showResult);
            }
        }

        SetActive(_rewardsFooter, view == View.Rewards);
        SetActive(_trainBuilderView, view == View.TrainBuilder);
        SetActive(_routeMapView, view == View.RouteMap);

        bool inStation = view == View.TrainBuilder || view == View.RouteMap;
        SetActive(_tabBar, inStation);

        SetTab(_trainBuilderTab, view == View.TrainBuilder);
        SetTab(_routeMapTab, view == View.RouteMap);
    }

    private void SetTab(Button tab, bool active)
    {
        if (tab == null || tab.targetGraphic == null)
        {
            return;
        }

        if (active)
        {
            tab.targetGraphic.color = _activeTabColor;
        }
        else
        {
            tab.targetGraphic.color = _inactiveTabColor;
        }
    }

    private static void SetActive(GameObject target, bool active)
    {
        if (target != null)
        {
            target.SetActive(active);
        }
    }
}
