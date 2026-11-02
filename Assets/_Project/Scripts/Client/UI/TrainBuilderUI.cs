using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
 
public sealed class TrainBuilderUI : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private GameObject _panel;
    [SerializeField] private Key _toggleKey = Key.T;
 
    [Tooltip("Only the host may edit the train.")]
    [SerializeField] private bool _hostOnly = false;
 
    [Header("List")]
    [Tooltip("Object with a Vertical Layout Group; rows are created inside it.")]
    [SerializeField] private Transform _rowContainer;
 
    [Tooltip("A disabled row inside the container, cloned for every wagon.")]
    [SerializeField] private TrainBuilderRow _rowTemplate;
 
    [SerializeField] private Button _addButton;
    [SerializeField] private TMP_Text _countLabel;
 
    [Header("New wagons")]
    [Tooltip("Registry id used for the front half of a newly added wagon.")]
    [SerializeField] private int _defaultFrontId = 0;
    [Tooltip("Registry id used for the back half of a newly added wagon.")]
    [SerializeField] private int _defaultBackId = 0;

    [Tooltip("Lobby: opens by itself once the train config exists. No key, no cursor locking, no player to freeze.")]
    [SerializeField] private bool _lobbyMode = true;
 
    private readonly List<TrainBuilderRow> _rows = new List<TrainBuilderRow>();
    private TrainConfig _config;
    private bool _open;
 
    private void Awake()
    {
        if (_panel != null) _panel.SetActive(false);
        if (_rowTemplate != null) _rowTemplate.gameObject.SetActive(false);
        if (_addButton != null) _addButton.onClick.AddListener(OnAddClicked);
    }
 
    private void OnDestroy()
    {
        if (_config != null) _config.LayoutChanged -= Refresh;
    }
 
    private void Update()
    {
        if (_config == null && TrainConfig.Instance != null)
        {
            _config = TrainConfig.Instance;
            _config.LayoutChanged += Refresh;
        }

        if (_lobbyMode)
        {
            if (!_open && _config != null && _config.IsSpawned) SetOpen(true);
            return;
        }
 
        Keyboard kb = Keyboard.current;
        if (kb == null) return;
 
        if (kb[_toggleKey].wasPressedThisFrame) SetOpen(!_open);
        else if (_open && kb[Key.Escape].wasPressedThisFrame) SetOpen(false);
    }
 
    private void SetOpen(bool open)
    {
        if (open)
        {
            if (_config == null || !_config.IsSpawned) return;
            if (_hostOnly && !NetworkManager.Singleton.IsHost) return;
            if (LocalPlayerIsBusy()) return;
        }
 
        _open = open;
        if (_panel != null) _panel.SetActive(open);
 
        if (!_lobbyMode)
        {
            SuspendLocalPlayer(open);
            Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = open;
        }
 
        if (open) Refresh();
    }
 
    // ----- drawing ---------------------------------------------------------
 
    private void Refresh()
    {
        if (!_open || _config == null || _rowTemplate == null) return;
 
        int count = _config.WagonCount;
 
        while (_rows.Count < count)
        {
            TrainBuilderRow row = Instantiate(_rowTemplate, _rowContainer);
            row.gameObject.SetActive(true);
            row.Init(_config.Registry, this);
            _rows.Add(row);
        }
 
        while (_rows.Count > count)
        {
            int last = _rows.Count - 1;
            Destroy(_rows[last].gameObject);
            _rows.RemoveAt(last);
        }
 
        for (int i = 0; i < count; i++) _rows[i].Show(i, _config.GetWagon(i));
 
        if (_addButton != null) _addButton.interactable = count < _config.MaxWagons;
        if (_countLabel != null) _countLabel.text = $"Wagons: {count}/{_config.MaxWagons}";
    }
 
    // ----- requests (called by rows) ---------------------------------------
 
    public void OnRowChanged(int index, int frontId, int backId)
    {
        if (_config != null) _config.SetWagonServerRpc(index, frontId, backId);
    }
 
    public void OnRowRemoved(int index)
    {
        if (_config != null) _config.RemoveWagonServerRpc(index);
    }
 
    private void OnAddClicked()
    {
        if (_config != null) _config.AddWagonServerRpc(_defaultFrontId, _defaultBackId);
    }
 
    // ----- local player ----------------------------------------------------
 
    private static GameObject LocalPlayer()
    {
        NetworkManager nm = NetworkManager.Singleton;
        NetworkObject obj = nm != null && nm.LocalClient != null ? nm.LocalClient.PlayerObject : null;
        return obj != null ? obj.gameObject : null;
    }
 
    private static bool LocalPlayerIsBusy()
    {
        GameObject player = LocalPlayer();
        if (player == null) return false;
 
        PlayerInteractor interactor = player.GetComponentInChildren<PlayerInteractor>();
        FirstPersonController controller = player.GetComponent<FirstPersonController>();
 
        return (interactor != null && interactor.Busy) || (controller != null && controller.ControlSuspended);
    }
 
    private static void SuspendLocalPlayer(bool suspend)
    {
        GameObject player = LocalPlayer();
        if (player == null) return;
 
        PlayerInteractor interactor = player.GetComponentInChildren<PlayerInteractor>();
        FirstPersonController controller = player.GetComponent<FirstPersonController>();
 
        if (interactor != null) interactor.Busy = suspend;
        if (controller != null) controller.ControlSuspended = suspend;
    }
}
