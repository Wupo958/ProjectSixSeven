using TMPro;
using Unity.Netcode;
using UnityEngine;
 
public sealed class DeathScreenUI : MonoBehaviour
{
    [Tooltip("Shown while the local player is dead.")]
    [SerializeField] private GameObject _deathPanel;
    [SerializeField] private TMP_Text _deathLabel;
 
    [Tooltip("Optional HP readout while alive.")]
    [SerializeField] private TMP_Text _healthLabel;
 
    private int _lastSecond = int.MinValue;
    private int _lastHealth = int.MinValue;
 
    private void Awake()
    {
        if (_deathPanel != null) _deathPanel.SetActive(false);
    }
 
    private void Update()
    {
        PlayerHealth player = PlayerHealth.Local;
        bool dead = player != null && player.IsDead;
 
        if (_deathPanel != null && _deathPanel.activeSelf != dead) _deathPanel.SetActive(dead);
        if (_healthLabel != null && _healthLabel.gameObject.activeSelf == dead)
            _healthLabel.gameObject.SetActive(!dead && player != null);
 
        if (player == null) return;
 
        if (dead) UpdateDeathText(player);
        else UpdateHealthText(player);
    }
 
    private void UpdateDeathText(PlayerHealth player)
    {
        int second = -1;
        if (player.RespawnAt >= 0.0 && NetworkManager.Singleton != null)
        {
            double left = player.RespawnAt - NetworkManager.Singleton.ServerTime.Time;
            second = Mathf.Max(0, Mathf.CeilToInt((float)left));
        }
 
        if (second == _lastSecond) return;
        _lastSecond = second;
        _lastHealth = int.MinValue;
 
        if (_deathLabel != null)
            _deathLabel.text = second >= 0 ? $"<size=150%><b>YOU DIED</b></size>\n<size=70%>Respawning in {second}s</size>" : "<size=150%><b>YOU DIED</b></size>\n<size=70%>Spectating until the end of the level</size>";
    }
 
    private void UpdateHealthText(PlayerHealth player)
    {
        _lastSecond = int.MinValue;
        if (_healthLabel == null || player.Health == _lastHealth) return;
 
        _lastHealth = player.Health;
        _healthLabel.text = $"HP {player.Health} / {player.MaxHealth}";
    }

    [ContextMenu("Debug: Kill me")]
    private void DebugKillMe()
    {
        if (PlayerHealth.Local != null) PlayerHealth.Local.DebugKill();
    }
}