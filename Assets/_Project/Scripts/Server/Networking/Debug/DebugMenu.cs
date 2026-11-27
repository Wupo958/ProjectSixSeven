using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
 
public sealed class DebugMenu : MonoBehaviour
{
    [SerializeField] private Key _toggleKey = Key.F1;
 
    [Tooltip("Scene shown after winning or losing.")]
    [SerializeField] private string _levelEndScene = "LevelEndScreen";
 
    [Header("Fallbacks (only if the game doesn't provide them)")]
    [Tooltip("Used when there is no active Item Registry.")]
    [SerializeField] private ItemRegistry _itemRegistry;
    [Tooltip("Used when the GameSession has no Enemy Registry (e.g. testing the scene on its own).")]
    [SerializeField] private EnemyRegistry _enemyRegistry;
 
    private static readonly string[] Tabs = { "Train", "Defects", "Weapons", "Maintenance", "Players", "Enemies", "Level" };
 
    private bool _open;
    private int _tab;
    private Vector2 _scroll;
    private Rect _window = new Rect(20, 20, 460, 640);
    private ulong _selectedPlayer;
 
    private WeaponBase[] _weapons = new WeaponBase[0];
    private MaintenanceTask[] _tasks = new MaintenanceTask[0];
    private float _nextRefresh;
 
    private static bool Allowed =>
        Debug.isDebugBuild && NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;
 
    // ----- open / close ------------------------------------------------------------
 
    private void Update()
    {
        if (!Allowed)
        {
            if (_open) SetOpen(false);
            return;
        }
 
        if (Keyboard.current != null && Keyboard.current[_toggleKey].wasPressedThisFrame)
            SetOpen(!_open);
 
        if (_open && Time.unscaledTime >= _nextRefresh)
        {
            _nextRefresh = Time.unscaledTime + 1f;
            _weapons = FindObjectsByType<WeaponBase>(FindObjectsSortMode.None);
            _tasks = FindObjectsByType<MaintenanceTask>(FindObjectsSortMode.None);
        }
    }
 
    private void SetOpen(bool open)
    {
        _open = open;
        _nextRefresh = 0f;
 
        FirstPersonController local = LocalController();
        if (local != null) local.InputBlocked = open;
 
        bool minigame = MinigameHost.Instance != null && MinigameHost.Instance.IsRunning;
        if (open)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else if (!minigame)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
 
    private static FirstPersonController LocalController()
    {
        NetworkObject player = NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClient != null
            ? NetworkManager.Singleton.LocalClient.PlayerObject : null;
        return player != null ? player.GetComponent<FirstPersonController>() : null;
    }
 
    // ----- drawing -------------------------------------------------------------------
 
    private void OnGUI()
    {
        if (!_open || !Allowed) return;
 
        float scale = Mathf.Max(1f, Screen.height / 1080f);
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
 
        _window = GUILayout.Window(67, _window, DrawWindow, $"Debug Menu  ({_toggleKey} to close)");
    }
 
    private void DrawWindow(int id)
    {
        _tab = GUILayout.SelectionGrid(_tab, Tabs, 4);
        GUILayout.Space(6);
 
        _scroll = GUILayout.BeginScrollView(_scroll);
        switch (_tab)
        {
            case 0: DrawTrain(); break;
            case 1: DrawDefects(); break;
            case 2: DrawWeapons(); break;
            case 3: DrawMaintenance(); break;
            case 4: DrawPlayers(); break;
            case 5: DrawEnemies(); break;
            case 6: DrawLevel(); break;
        }
        GUILayout.EndScrollView();
 
        GUI.DragWindow(new Rect(0, 0, 10000, 20));
    }
 
    // ----- tabs ------------------------------------------------------------------------
 
    private void DrawTrain()
    {
        TrainHealth train = TrainHealth.Instance;
 
        DebugCheats.TrainGodMode = GUILayout.Toggle(DebugCheats.TrainGodMode, " God mode (train takes no damage)");
 
        Section("Lok");
        if (train != null)
        {
            GUILayout.Label($"HP {train.LokHp} / {train.LokMaxHp}");
            Row(("-10%", () => train.DebugSetLokHp(train.LokHp - train.LokMaxHp / 10)),
                ("Full", () => train.DebugSetLokHp(train.LokMaxHp)));
        }
 
        Section("Speed");
        TrainMotion motion = TrainMotion.Instance;
        if (motion != null)
        {
            GUILayout.Label($"{motion.Speed:0.0} m/s  (target {motion.TargetSpeed:0.0})");
            Row(("Stop", () => motion.RequestTargetSpeed(0f)),
                ("Half", () => motion.RequestTargetSpeed(motion.MaxSpeed * 0.5f)),
                ("Full", () => motion.RequestTargetSpeed(motion.MaxSpeed)));
        }
 
        Section("Fuel");
        LokFuel fuel = LokFuel.Instance;
        if (fuel != null)
        {
            GUILayout.Label($"{fuel.Fuel:0} / {fuel.Capacity:0}  ({fuel.Fraction * 100f:0}%)");
            Row(("Empty", () => fuel.RequestSetFuel(0f)),
                ("+1 Item", () => fuel.RequestRefuel()),
                ("Fill", () => fuel.RequestSetFuel(fuel.Capacity)));
        }
    }
 
    private void DrawDefects()
    {
        TrainHealth train = TrainHealth.Instance;
        if (train == null) { GUILayout.Label("No train."); return; }
 
        for (int w = 0; w < train.WagonCount; w++)
        {
            int wagon = w;
            int defects = train.DefectsOf(wagon);
 
            if (train.IsWagonDestroyed(wagon))
            {
                Section($"Wagon {wagon + 1} - DESTROYED");
                continue;
            }
 
            int broken = 0;
            for (int p = 0; p < defects; p++) if (train.IsDefectBroken(wagon, p)) broken++;
            Section($"Wagon {wagon + 1}   {broken} / {defects} defects active");
 
            GUILayout.BeginHorizontal();
            for (int p = 0; p < defects; p++)
            {
                int point = p;
                bool isBroken = train.IsDefectBroken(wagon, point);
                Color old = GUI.backgroundColor;
                GUI.backgroundColor = isBroken ? new Color(1f, 0.35f, 0.3f) : Color.white;
                if (GUILayout.Button((point + 1).ToString(), GUILayout.Width(30)))
                {
                    if (isBroken) train.RepairDefect(wagon, point);
                    else train.ForceBreak(wagon, point);
                }
                GUI.backgroundColor = old;
            }
            GUILayout.EndHorizontal();
 
            Row(("Repair all", () =>
                {
                    for (int p = 0; p < defects; p++)
                        if (train.IsDefectBroken(wagon, p)) train.RepairDefect(wagon, p);
                }),
                ("Destroy wagon", () => train.ForceDestroyWagon(wagon)));
        }
    }
 
    private void DrawWeapons()
    {
        if (_weapons.Length == 0) { GUILayout.Label("No weapons."); return; }
 
        foreach (WeaponBase weapon in _weapons)
        {
            if (weapon == null || !weapon.IsSpawned) continue;
 
            string name = weapon.Definition != null ? weapon.Definition.DisplayName : weapon.name;
            string wear = weapon.IsJammed ? "FAILED" : $"wear {weapon.Wear * 100f:0}%";
            Section($"{name}  ({wear})");
 
            Row(("+25% wear", () => weapon.DebugAddWear(0.25f)),
                ("Maintain", () => weapon.RequestMaintenance()));
 
            WeaponFault faults = weapon.Faults;
            WeaponFault edited = faults;
            GUILayout.BeginHorizontal();
            edited = FaultToggle(edited, WeaponFault.PitchLocked, "Pitch locked");
            edited = FaultToggle(edited, WeaponFault.YawLocked, "Yaw locked");
            edited = FaultToggle(edited, WeaponFault.TriggerJammed, "Trigger jammed");
            GUILayout.EndHorizontal();
            if (edited != faults) weapon.DebugSetFaults(edited);
        }
    }
 
    private void DrawMaintenance()
    {
        if (_tasks.Length == 0) { GUILayout.Label("No maintenance tasks."); return; }
 
        foreach (MaintenanceTask task in _tasks)
        {
            if (task == null) continue;
            string state = task.IsActive ? $"DUE - {task.Severity * 100f:0}%" : "OK";
            Section($"{task.DisplayName}  ({state})");
            Row(("Make due", () => task.DebugSetActive(true)),
                ("Clear", () => task.DebugSetActive(false)));
        }
    }
 
    private void DrawPlayers()
    {
        DebugCheats.PlayerGodMode = GUILayout.Toggle(DebugCheats.PlayerGodMode,
            " God mode (players take no damage; falling off still kills)");
 
        Section("Players");
        NetworkObject selected = null;
        foreach (NetworkClient client in NetworkManager.Singleton.ConnectedClientsList)
        {
            NetworkObject obj = client.PlayerObject;
            if (obj == null) continue;
 
            PlayerHealth health = obj.GetComponent<PlayerHealth>();
            string label = client.ClientId == NetworkManager.ServerClientId ? "Host" : $"Client {client.ClientId}";
            string hp = health == null ? "" : health.IsDead ? "  DEAD" : $"  HP {health.Health}/{health.MaxHealth}";
 
            bool isSelected = client.ClientId == _selectedPlayer;
            if (GUILayout.Toggle(isSelected, $" {label}{hp}") && !isSelected) _selectedPlayer = client.ClientId;
            if (client.ClientId == _selectedPlayer) selected = obj;
        }
        if (selected == null) { _selectedPlayer = NetworkManager.ServerClientId; return; }
 
        PlayerHealth target = selected.GetComponent<PlayerHealth>();
        if (target != null)
        {
            Row(("Kill", () => target.DebugKill()),
                ("-100 HP", () => target.ApplyDamage(100f, target.transform.position, PlayerHealth.EnvironmentAttacker)));
        }
 
        Section("Give item (into the hand)");
        ItemRegistry items = ItemRegistry.Active != null ? ItemRegistry.Active : _itemRegistry;
        PlayerInventory inventory = selected.GetComponentInChildren<PlayerInventory>();
        if (items != null && inventory != null)
        {
            ulong clientId = _selectedPlayer;
            Grid(items.Items.Count, i =>
            {
                ItemDefinition item = items.Get(i);
                if (item == null) return;
                if (GUILayout.Button(item.DisplayName))
                    inventory.GrantItemClientRpc(i, To(clientId));
            });
        }
        else GUILayout.Label("No Item Registry found.");
 
        Section("Status effects");
        StatusEffectRegistry effects = StatusEffectRegistry.Instance;
        StatusEffectReceiver receiver = selected.GetComponent<StatusEffectReceiver>();
        if (effects != null && receiver != null)
        {
            Grid(effects.Count, i =>
            {
                StatusEffect effect = effects.Get(i);
                if (effect != null && GUILayout.Button(effect.DisplayName)) receiver.Apply(effect);
            });
            if (GUILayout.Button("Clear all effects")) receiver.ClearAll();
        }
        else GUILayout.Label("No Status Effect Registry or receiver found.");
    }
 
    private void DrawEnemies()
    {
        EnemySpawner spawner = FindFirstObjectByType<EnemySpawner>();
        if (spawner == null) { GUILayout.Label("No EnemySpawner in the scene."); return; }
 
        Section("Spawn a group");
        EnemyRegistry enemies = GameSession.Instance != null && GameSession.Instance.EnemyRegistry != null
            ? GameSession.Instance.EnemyRegistry : _enemyRegistry;
 
        if (enemies != null)
        {
            Grid(enemies.Count, i =>
            {
                EnemyBase enemy = enemies.Get(i);
                if (enemy != null && GUILayout.Button(enemy.DisplayName)) spawner.DebugSpawnGroup(enemy);
            });
        }
        else GUILayout.Label("No Enemy Registry found.");
 
        Section("Waves");
        Row(("Spawn wave now", () => spawner.DebugSpawnWave()),
            ("Kill all enemies", () => EnemyBase.DespawnAll()));
    }
 
    private void DrawLevel()
    {
        GUILayout.Label("Ends the level for everyone.");
        Row(("Win", () => EndLevel(true)),
            ("Lose", () => { if (TrainHealth.Instance != null) TrainHealth.Instance.DebugTriggerGameOver(); }));
        GUILayout.Label("Skipping a level counts as winning it.");
        if (GUILayout.Button("Skip level")) EndLevel(true);
    }
 
    private void EndLevel(bool won)
    {
        SetOpen(false);
        if (PlayerLifecycle.Main != null) PlayerLifecycle.Main.DespawnAllPlayers();
        EnemyBase.DespawnAll();
        if (GameSession.Instance != null) GameSession.Instance.EndLevel(won);
        NetworkManager.Singleton.SceneManager.LoadScene(_levelEndScene, LoadSceneMode.Single);
    }
 
    // ----- small GUI helpers ---------------------------------------------------------
 
    private static GUIStyle s_header;
 
    private static void Section(string text)
    {
        if (s_header == null) s_header = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
        GUILayout.Space(8);
        GUILayout.Label(text, s_header);
    }
 
    private static void Row(params (string label, System.Action action)[] buttons)
    {
        GUILayout.BeginHorizontal();
        foreach (var (label, action) in buttons)
            if (GUILayout.Button(label)) action();
        GUILayout.EndHorizontal();
    }
 
    private static void Grid(int count, System.Action<int> drawButton)
    {
        for (int i = 0; i < count; i += 3)
        {
            GUILayout.BeginHorizontal();
            for (int j = i; j < Mathf.Min(i + 3, count); j++) drawButton(j);
            GUILayout.EndHorizontal();
        }
    }
 
    private static WeaponFault FaultToggle(WeaponFault faults, WeaponFault fault, string label)
    {
        bool on = GUILayout.Toggle((faults & fault) != 0, " " + label);
        return on ? faults | fault : faults & ~fault;
    }
 
    private static ClientRpcParams To(ulong clientId) => new ClientRpcParams
    {
        Send = new ClientRpcSendParams { TargetClientIds = new List<ulong> { clientId } }
    };
}