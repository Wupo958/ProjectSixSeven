using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
 
//"Mash the button X times." Difficulty = required presses.
public sealed class MashMinigame : MinigameBase
{
    [Header("Input")]
    [SerializeField] private Key _mashKey = Key.Space;
    [SerializeField] private Key _cancelKey = Key.Escape;
 
    [Header("UI")]
    [SerializeField] private TMP_Text _label;
    [SerializeField] private Slider _progressBar;
 
    [Header("Rules")]
    [Tooltip("Seconds allowed before the attempt fails. 0 = no limit.")]
    [SerializeField] private float _timeLimit = 0f;
 
    [Tooltip("Progress lost per second, so you have to keep mashing.")]
    [SerializeField] private float _decayPerSecond = 0f;
 
    private float _presses;
    private float _timeLeft;
 
    protected override void OnBegin()
    {
        _presses = 0f;
        _timeLeft = _timeLimit;
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
 
        if (Keyboard.current[_mashKey].wasPressedThisFrame)
            _presses += 1f;
 
        if (_decayPerSecond > 0f)
            _presses = Mathf.Max(0f, _presses - _decayPerSecond * Time.deltaTime);
 
        if (_timeLimit > 0f)
        {
            _timeLeft -= Time.deltaTime;
            if (_timeLeft <= 0f) { Cancel(); return; }
        }
 
        Refresh();
 
        if (_presses >= Difficulty) Succeed();
    }
 
    private void Refresh()
    {
        float t = Mathf.Clamp01(_presses / Difficulty);
 
        if (_progressBar != null) _progressBar.value = t;
 
        if (_label != null)
        {
            _label.text = _timeLimit > 0f
                ? $"Mash [{_mashKey}]!  {Mathf.FloorToInt(_presses)}/{Difficulty}   {_timeLeft:0.0}s"
                : $"Mash [{_mashKey}]!  {Mathf.FloorToInt(_presses)}/{Difficulty}";
        }
    }
}
