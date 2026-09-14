using System;
using UnityEngine;

//scene-level registry of minigame UIs. Put this on a UI object and drag every
//minigame (each on its own disabled child) into the list.
public sealed class MinigameHost : MonoBehaviour
{
    public static MinigameHost Instance { get; private set; }
 
    [Tooltip("Every minigame UI in the scene. Each should start disabled.")]
    [SerializeField] private MinigameBase[] _minigames;
 
    public bool IsRunning { get; private set; }
 
    private void Awake()
    {
        Instance = this;
        foreach (MinigameBase m in _minigames)
            if (m != null) m.gameObject.SetActive(false);
    }
 
    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
 
    public bool Run(MinigameType type, int difficulty, PlayerInteractor player, Action onSuccess)
    {
        if (IsRunning) return false;
 
        MinigameBase game = Find(type);
        if (game == null)
        {
            Debug.LogWarning($"No minigame registered for {type}.", this);
            return false;
        }
 
        IsRunning = true;
        if (player != null) player.Busy = true;
 
        //free the cursor for UI-based minigames.
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
 
        game.Begin(difficulty,
            () => { Finish(player); onSuccess?.Invoke(); },
            () => Finish(player));
 
        return true;
    }
 
    private void Finish(PlayerInteractor player)
    {
        IsRunning = false;
        if (player != null) player.Busy = false;
 
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
 
    private MinigameBase Find(MinigameType type)
    {
        foreach (MinigameBase m in _minigames)
            if (m != null && m.Type == type) return m;
        return null;
    }
}
