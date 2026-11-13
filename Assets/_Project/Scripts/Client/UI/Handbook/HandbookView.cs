using System.Collections.Generic;
using System.Text.RegularExpressions;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
 
public sealed class HandbookView : MonoBehaviour
{
    public static HandbookView Instance { get; private set; }
 
    public static HandbookPage ContextOverride { get; set; }
 
    [Header("Content")]
    [Tooltip("Shown when the player isn't looking at or manning anything: basic controls.")]
    [SerializeField] private HandbookPage _defaultPage;
    [SerializeField] private Key _toggleKey = Key.Tab;
 
    [Header("Layout")]
    [Tooltip("The panel that is shown and hidden.")]
    [SerializeField] private GameObject _panel;
    [SerializeField] private TMP_Text _title;
 
    [Tooltip("Left page area. Needs a Vertical Layout Group (Control Child Size ticked, Force Expand off).")]
    [SerializeField] private RectTransform _leftColumn;
    [SerializeField] private RectTransform _rightColumn;
 
    [Header("Templates (keep them disabled; style them however you like)")]
    [SerializeField] private TMP_Text _headingTemplate;
    [SerializeField] private TMP_Text _bodyTemplate;
    [SerializeField] private Image _imageTemplate;
    [SerializeField] private TMP_Text _controlTemplate;
 
    [Header("Formatting")]
    [Tooltip("Colour for [Key] highlights.")]
    [SerializeField] private Color _keyColor = new Color(1f, 0.82f, 0.35f);
 
    [Tooltip("Where the action column starts in control rows, in % of the page width.")]
    [Range(10f, 70f)]
    [SerializeField] private float _controlActionColumn = 35f;
 
    [Tooltip("Tallest an image may get, in pixels.")]
    [SerializeField] private float _maxImageHeight = 120f;
 
    [Tooltip("Text shrinks to fit long pages, but never below this fraction of its template size.")]
    [Range(0.4f, 1f)]
    [SerializeField] private float _minTextScale = 0.6f;
 
    private const string GeneratedName = "__handbook";
    private static readonly Regex KeyPattern = new Regex(@"\[([^\[\]\r\n]{1,16})\]");
 
    private readonly List<(TMP_Text text, float baseSize)> _scaled = new List<(TMP_Text, float)>();
 
    public HandbookPage Shown { get; private set; }
    public float LeftScale { get; private set; } = 1f;
    public float RightScale { get; private set; } = 1f;
    public bool Overflowing { get; private set; }
    public bool IsOpen => _panel != null && _panel.activeSelf;
 
    private void OnEnable()
    {
        Instance = this;
        if (Application.isPlaying) Hide();
    }
 
    private void OnDisable()
    {
        if (Instance == this) Instance = null;
    }
 
    private void Update()
    {
        if (!Application.isPlaying || Keyboard.current == null) return;
        if (!Keyboard.current[_toggleKey].wasPressedThisFrame) return;
 
        if (IsOpen) Hide();
        else Show(ContextPage());
    }
 
    // ----- what to show -------------------------------------------------------
 
    private HandbookPage ContextPage()
    {
        if (ContextOverride != null) return ContextOverride;
 
        NetworkManager nm = NetworkManager.Singleton;
        NetworkObject player = nm != null && nm.LocalClient != null ? nm.LocalClient.PlayerObject : null;
        PlayerInteractor interactor = player != null ? player.GetComponentInChildren<PlayerInteractor>() : null;
 
        if (interactor != null && interactor.Current != null)
        {
            HandbookTopic topic = interactor.Current.GetComponentInParent<HandbookTopic>();
            if (topic != null && topic.Page != null) return topic.Page;
        }
 
        return _defaultPage;
    }
 
    // ----- building -----------------------------------------------------------
 
    public void Show(HandbookPage page)
    {
        if (page == null || _panel == null) return;
 
        Shown = page;
        _panel.SetActive(true);
 
        if (_title != null) _title.text = Format(page.Title);
 
        LeftScale = Build(_leftColumn, page.Left, out bool leftOverflow);
        RightScale = Build(_rightColumn, page.Right, out bool rightOverflow);
        Overflowing = leftOverflow || rightOverflow;
    }
 
    public void Hide()
    {
        Clear(_leftColumn);
        Clear(_rightColumn);
        Shown = null;
        if (_panel != null) _panel.SetActive(false);
    }
 
    private float Build(RectTransform column, List<HandbookSection> sections, out bool overflow)
    {
        overflow = false;
        if (column == null) return 1f;
 
        Clear(column);
        _scaled.Clear();
 
        if (sections != null)
        {
            foreach (HandbookSection s in sections)
            {
                if (s == null) continue;
 
                if (!string.IsNullOrWhiteSpace(s.Heading))
                    AddText(_headingTemplate, column, s.Heading);
 
                if (s.Image != null)
                    AddImage(column, s.Image);
 
                if (!string.IsNullOrWhiteSpace(s.Body))
                    AddText(_bodyTemplate, column, s.Body);
 
                if (s.Controls != null)
                {
                    foreach (HandbookControl c in s.Controls)
                    {
                        if (c == null || string.IsNullOrWhiteSpace(c.Key)) continue;
                        string key = c.Key.Trim().Trim('[', ']');
                        AddText(_controlTemplate, column,
                                $"[{key}]<pos={_controlActionColumn:0.#}%>{c.Action}");
                    }
                }
            }
        }
 
        return Fit(column, out overflow);
    }
 
    private void AddText(TMP_Text template, RectTransform column, string raw)
    {
        if (template == null) return;
        TMP_Text t = Spawn(template, column);
        t.text = Format(raw);
        _scaled.Add((t, template.fontSize));
    }
 
    private void AddImage(RectTransform column, Sprite sprite)
    {
        if (_imageTemplate == null) return;
        Image img = Spawn(_imageTemplate, column);
        img.sprite = sprite;
        img.preserveAspect = true;
 
        float width = column.rect.width;
        float aspect = sprite.rect.width > 0f ? sprite.rect.height / sprite.rect.width : 1f;
 
        LayoutElement le = img.GetComponent<LayoutElement>();
        if (le == null) le = img.gameObject.AddComponent<LayoutElement>();
        le.preferredHeight = Mathf.Min(_maxImageHeight, width * aspect);
    }
 
    private T Spawn<T>(T template, RectTransform column) where T : Component
    {
        T clone = Instantiate(template, column);
        clone.gameObject.name = GeneratedName;
        clone.gameObject.SetActive(true);
        if (!Application.isPlaying) clone.gameObject.hideFlags = HideFlags.DontSave;
        return clone;
    }
 
    private static void Clear(RectTransform column)
    {
        if (column == null) return;
        for (int i = column.childCount - 1; i >= 0; i--)
        {
            GameObject child = column.GetChild(i).gameObject;
            if (child.name != GeneratedName) continue;
            if (Application.isPlaying) Destroy(child);
            else DestroyImmediate(child);
        }
    }
 
    // ----- fitting ------------------------------------------------------------
 
    private float Fit(RectTransform column, out bool overflow)
    {
        float available = column.rect.height;
        float scale = 1f;
        overflow = false;
 
        for (int i = 0; i < 10; i++)
        {
            foreach (var (text, baseSize) in _scaled) text.fontSize = baseSize * scale;
 
            LayoutRebuilder.ForceRebuildLayoutImmediate(column);
            float needed = LayoutUtility.GetPreferredHeight(column);
 
            if (needed <= available) return scale;
            if (scale <= _minTextScale) { overflow = true; return scale; }
 
            float ratio = Mathf.Clamp(available / needed, 0.8f, 0.97f);
            scale = Mathf.Max(_minTextScale, scale * ratio);
        }
 
        overflow = true;
        return scale;
    }
 
    // ----- autoformatting -------------------------------------------------------
 
    private string Format(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return string.Empty;
        string hex = ColorUtility.ToHtmlStringRGB(_keyColor);
        return KeyPattern.Replace(raw, m => $"<b><color=#{hex}>[{m.Groups[1].Value}]</color></b>");
    }
}
