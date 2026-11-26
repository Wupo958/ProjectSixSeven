using UnityEngine;
using UnityEngine.UI;

public sealed class StatusEffectScreen : MonoBehaviour
{
    [Tooltip("Full-screen Image. Its colour is set by the active effects.")]
    [SerializeField] private Image _overlay;

    private Color _shown = Color.clear;

    private void Awake()
    {
        if (_overlay != null) _overlay.enabled = false;
    }

    private void Update()
    {
        if (_overlay == null) return;

        StatusEffectReceiver local = StatusEffectReceiver.Local;
        Color color = local != null ? local.Mods.ScreenOverlay : Color.clear;
        if (color == _shown) return;

        _shown = color;
        _overlay.color = color;
        _overlay.enabled = color.a > 0.001f;
    }
}
