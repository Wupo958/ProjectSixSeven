using System;
using UnityEngine;
 
public sealed class WagonStatusPanel : MonoBehaviour
{
    [Header("Lamps")]
    [Tooltip("One lamp. Needs a Renderer on it or a child. No collider.")]
    [SerializeField] private GameObject _lampPrefab;
    [SerializeField] private Material _lampOff;
    [SerializeField] private Material _lampOn;
 
    [Tooltip("Lamps are laid out along this transform's X axis, centred.")]
    [SerializeField] private Transform _lampRow;
    [SerializeField] private float _lampSpacing = 0.12f;
 
    [Header("Backplate (optional)")]
    [Tooltip("Stretched along X to fit the lamps.")]
    [SerializeField] private Transform _backplate;
    [SerializeField] private float _backplatePadding = 0.06f;
 
    private TrainHealth _train;
    private int _wagon = -1;
    private bool _isLok;
    private Renderer[] _lamps = Array.Empty<Renderer>();
    private int _shownActive = -1;
 
    public void Bind(TrainHealth train, int wagonIndex, int defectCount)
    {
        Unsubscribe();
 
        if (train != null && Application.isPlaying && train.IsWagonDestroyed(wagonIndex))
        {
            Destroy(gameObject);
            return;
        }
 
        _train = train;
        _wagon = wagonIndex;
        _isLok = false;
        BuildLamps(defectCount);
 
        if (_train != null)
        {
            _train.OnWagonChanged += OnWagonChanged;
            _train.OnWagonDestroyed += OnWagonDestroyed;
        }
        _shownActive = -1;
        Refresh();
    }
 
    public void BindLok(TrainHealth train, int lampCount)
    {
        Unsubscribe();
 
        _train = train;
        _wagon = -1;
        _isLok = true;
        BuildLamps(lampCount);
 
        if (_train != null) _train.OnLokChanged += Refresh;
        _shownActive = -1;
        Refresh();
    }
 
    private void OnDestroy() => Unsubscribe();
 
    private void Unsubscribe()
    {
        if (_train == null) return;
        _train.OnWagonChanged -= OnWagonChanged;
        _train.OnWagonDestroyed -= OnWagonDestroyed;
        _train.OnLokChanged -= Refresh;
    }
 
    private void OnWagonChanged(int wagon)
    {
        if (!_isLok && wagon == _wagon) Refresh();
    }
 
    private void OnWagonDestroyed(int wagon)
    {
        if (!_isLok && wagon == _wagon) Destroy(gameObject);
    }
 
    private void Refresh()
    {
        int active = 0;
        if (_train != null && Application.isPlaying)
        {
            if (_isLok)
            {
                int max = Mathf.Max(1, _train.LokMaxHp);
                int lost = Mathf.Clamp(max - _train.LokHp, 0, max);
                active = lost * _lamps.Length / max;
            }
            else
            {
                for (int p = 0; p < _lamps.Length; p++)
                    if (_train.IsDefectBroken(_wagon, p)) active++;
            }
        }
 
        if (active == _shownActive) return;
        _shownActive = active;
 
        for (int i = 0; i < _lamps.Length; i++)
            if (_lamps[i] != null) _lamps[i].sharedMaterial = i < active ? _lampOn : _lampOff;
    }
 
    private void BuildLamps(int count)
    {
        Transform row = _lampRow != null ? _lampRow : transform;
 
        for (int i = row.childCount - 1; i >= 0; i--)
        {
            GameObject old = row.GetChild(i).gameObject;
            if (Application.isPlaying) Destroy(old); else DestroyImmediate(old);
        }
 
        _lamps = new Renderer[Mathf.Max(0, count)];
        if (_lampPrefab == null) return;
 
        float start = -(count - 1) * 0.5f * _lampSpacing;
        for (int i = 0; i < count; i++)
        {
            GameObject lamp = Instantiate(_lampPrefab, row);
            lamp.name = $"Lamp {i + 1}";
            lamp.transform.localPosition = new Vector3(start + i * _lampSpacing, 0f, 0f);
            lamp.transform.localRotation = Quaternion.identity;
            _lamps[i] = lamp.GetComponentInChildren<Renderer>();
        }
 
        if (_backplate != null)
        {
            Vector3 scale = _backplate.localScale;
            scale.x = count * _lampSpacing + _backplatePadding * 2f;
            _backplate.localScale = scale;
        }
    }
}