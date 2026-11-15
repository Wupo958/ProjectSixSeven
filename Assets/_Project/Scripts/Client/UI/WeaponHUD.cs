using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class WeaponHUD : MonoBehaviour
{
    [Tooltip("Panel shown while manning a weapon.")]
    [SerializeField] private GameObject _panel;
 
    [SerializeField] private TMP_Text _weaponName;
 
    [Tooltip("Loaded rounds and capacity, e.g. 87 / 100.")]
    [SerializeField] private TMP_Text _ammoCount;
 
    [Tooltip("Name of the loaded ammo item, or EMPTY.")]
    [SerializeField] private TMP_Text _ammoType;
 
    [Tooltip("Weapon-specific readout (e.g. FLAK fuse distance). Hidden when the weapon has none.")]
    [SerializeField] private TMP_Text _readout;
 
    [Header("Magazine bar (large magazines)")]
    [Tooltip("The bar's root, e.g. its dark track. Hidden when pips are used.")]
    [SerializeField] private GameObject _barRoot;
 
    [Tooltip("The fill. Image Type Sliced keeps rounded ends (its width is set via anchors); Filled works too.")]
    [SerializeField] private Image _barFill;
 
    [Header("Magazine pips (small magazines)")]
    [Tooltip("Object with a Horizontal Layout Group that holds the pips.")]
    [SerializeField] private RectTransform _pipContainer;
 
    [Tooltip("A disabled Image inside the pip container, cloned once per round.")]
    [SerializeField] private Image _pipTemplate;
 
    [Tooltip("Magazines up to this size show pips instead of the bar.")]
    [SerializeField] private int _maxPips = 10;
 
    [Header("Colours")]
    [SerializeField] private Color _normalColor = Color.white;
    [SerializeField] private Color _lowColor = new Color(1f, 0.35f, 0.3f);
    [SerializeField] private Color _fullColor = new Color(1f, 0.72f, 0.25f);
    [SerializeField] private Color _emptySlotColor = new Color(1f, 1f, 1f, 0.15f);
 
    [Tooltip("At or below this share of the magazine, the count turns to the low colour.")]
    [Range(0f, 1f)]
    [SerializeField] private float _lowAmmoShare = 0.2f;
 
    private readonly List<Image> _pips = new List<Image>();
    private WeaponBase _shown;
    private bool _usePips;
    private int _lastLoaded;
    private ItemDefinition _lastAmmo;
    private bool _ammoShown;
    private string _lastReadout;
 
    private void Awake()
    {
        if (_panel != null) _panel.SetActive(false);
        if (_pipTemplate != null) _pipTemplate.gameObject.SetActive(false);
    }
 
    private void Update()
    {
        WeaponBase weapon = WeaponBase.LocalManned;
        bool show = weapon != null && weapon.IsSpawned;
 
        if (_panel != null && _panel.activeSelf != show) _panel.SetActive(show);
 
        if (!show)
        {
            _shown = null;
            return;
        }
 
        if (weapon != _shown) ShowWeapon(weapon);
        Refresh(weapon);
    }
 
    private void ShowWeapon(WeaponBase weapon)
    {
        _shown = weapon;
        _lastLoaded = -1;
        _lastAmmo = null;
        _ammoShown = false;
        _lastReadout = "\0";
 
        if (_weaponName != null)
            _weaponName.text = weapon.Definition != null ? weapon.Definition.DisplayName : weapon.name;
 
        _usePips = _pipContainer != null && _pipTemplate != null && weapon.Capacity <= _maxPips;
        BuildPips(_usePips ? weapon.Capacity : 0);
 
        if (_pipContainer != null) _pipContainer.gameObject.SetActive(_usePips);
        GameObject bar = _barRoot != null ? _barRoot : _barFill != null ? _barFill.gameObject : null;
        if (bar != null) bar.SetActive(!_usePips);
    }
 
    private void BuildPips(int count)
    {
        foreach (Image pip in _pips)
            if (pip != null) Destroy(pip.gameObject);
        _pips.Clear();
 
        for (int i = 0; i < count; i++)
        {
            Image pip = Instantiate(_pipTemplate, _pipContainer);
            pip.gameObject.SetActive(true);
            _pips.Add(pip);
        }
    }
 
    private void Refresh(WeaponBase weapon)
    {
        int loaded = weapon.Loaded;
        int capacity = weapon.Capacity;
        ItemDefinition ammo = loaded > 0 ? weapon.LoadedAmmo : null;
 
        if (loaded != _lastLoaded)
        {
            _lastLoaded = loaded;
            bool low = loaded == 0 || (capacity > 0 && (float)loaded / capacity <= _lowAmmoShare);
 
            if (_ammoCount != null)
            {
                _ammoCount.text = $"{loaded} / {capacity}";
                _ammoCount.color = low ? _lowColor : _normalColor;
            }
 
            if (_usePips) UpdatePips(loaded);
            else UpdateBar(capacity > 0 ? (float)loaded / capacity : 0f, low);
 
            _ammoShown = false;
        }
 
        if (ammo != _lastAmmo || !_ammoShown)
        {
            _lastAmmo = ammo;
            _ammoShown = true;
            if (_ammoType != null)
            {
                _ammoType.text = ammo != null ? ammo.DisplayName : "EMPTY";
                _ammoType.color = ammo != null ? _normalColor : _lowColor;
            }
        }
 
        string readout = weapon.HudReadout;
        if (readout != _lastReadout)
        {
            _lastReadout = readout;
            if (_readout != null)
            {
                bool has = !string.IsNullOrEmpty(readout);
                if (_readout.gameObject.activeSelf != has) _readout.gameObject.SetActive(has);
                _readout.text = readout ?? string.Empty;
            }
        }
    }
 
    private void UpdatePips(int loaded)
    {
        for (int i = 0; i < _pips.Count; i++)
            _pips[i].color = i < loaded ? _fullColor : _emptySlotColor;
    }
 
    private void UpdateBar(float fraction, bool low)
    {
        if (_barFill == null) return;
 
        _barFill.color = low ? _lowColor : _fullColor;
 
        if (_barFill.type == Image.Type.Filled)
        {
            _barFill.fillAmount = fraction;
        }
        else
        {
            RectTransform rt = _barFill.rectTransform;
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(Mathf.Max(fraction, 0.0001f), 1f);
            _barFill.enabled = fraction > 0f;
        }
    }
}