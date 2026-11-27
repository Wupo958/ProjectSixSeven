using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class HoldMinigame : MinigameBase
{
    [Header("Input")]
    [SerializeField] private Key _holdKey = Key.Space;
    [SerializeField] private Key _cancelKey = Key.Escape;
 
    [Header("UI")]
    [SerializeField] private TMP_Text _label;
    [SerializeField] private Slider _progressBar;
 
    [Tooltip("Shown in the label: \"Hold [Space] to <action>\".")]
    [SerializeField] private string _action = "pour fuel";
 
    [Header("Rules")]
    [Tooltip("Progress lost per second while not holding (0 = it just pauses). 1 = a full bar per second.")]
    [SerializeField, Min(0f)] private float _drainPerSecond = 0f;
 
    private float _progress; // 0..1
    private int _shownPercent = -1;
 
    protected override void OnBegin()
    {
        _progress = 0f;
        _shownPercent = -1;
        Refresh();
    }
 
    private void Update()
    {
        if (Keyboard.current == null) return;
 
        if (Keyboard.current[_cancelKey].wasPressedThisFrame)
        {
            Cancel();
            return;
        }
 
        if (Keyboard.current[_holdKey].isPressed)
            _progress += Time.deltaTime / Difficulty;
        else
            _progress -= _drainPerSecond * Time.deltaTime;
 
        _progress = Mathf.Clamp01(_progress);
        Refresh();
 
        if (_progress >= 1f) Succeed();
    }
 
    private void Refresh()
    {
        if (_progressBar != null) _progressBar.value = _progress;
 
        int percent = Mathf.FloorToInt(_progress * 100f);
        if (_label == null || percent == _shownPercent) return;
 
        _shownPercent = percent;
        _label.text = $"Hold [{_holdKey}] to {_action}   {percent}%\n<size=70%>[{_cancelKey}] cancel</size>";
    }
}