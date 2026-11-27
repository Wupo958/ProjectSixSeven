using TMPro;
using UnityEngine;
using UnityEngine.UI;
 
public sealed class FuelHUD : MonoBehaviour
{
    [Tooltip("e.g. FUEL 65%")]
    [SerializeField] private TMP_Text _label;
 
    [Tooltip("The fill inside the track. Image Type Sliced: its width is set via anchors, " +
             "which keeps the rounded ends.")]
    [SerializeField] private Image _fill;
 
    [Header("Colours")]
    [SerializeField] private Color _normalColor = Color.white;
    [SerializeField] private Color _fullColor = new Color(1f, 0.72f, 0.25f);
    [SerializeField] private Color _lowColor = new Color(1f, 0.35f, 0.3f);
 
    [Tooltip("At or below this share of the tank, label and bar turn to the low colour.")]
    [SerializeField, Range(0f, 1f)] private float _lowShare = 0.25f;
 
    private int _shownPercent = -1;
 
    private void Update()
    {
        LokFuel tank = LokFuel.Instance;
        if (tank == null) return;
 
        float fraction = tank.Fraction;
        int percent = Mathf.CeilToInt(fraction * 100f);
        if (percent == _shownPercent) return;
        _shownPercent = percent;
 
        bool low = fraction <= _lowShare;
 
        if (_label != null)
        {
            _label.text = percent > 0 ? $"FUEL {percent}%" : "FUEL EMPTY";
            _label.color = low ? _lowColor : _normalColor;
        }
 
        if (_fill != null)
        {
            _fill.color = low ? _lowColor : _fullColor;
 
            RectTransform rt = _fill.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = new Vector2(Mathf.Max(fraction, 0.0001f), 1f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            _fill.enabled = fraction > 0f;
        }
    }
}