using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
 
public sealed class TrainBuilderRow : MonoBehaviour
{
    [SerializeField] private TMP_Text _label;
    [SerializeField] private TMP_Dropdown _frontDropdown;
    [SerializeField] private TMP_Dropdown _backDropdown;
    [SerializeField] private Button _removeButton;
 
    private TrainBuilderUI _owner;
    private int _index;
 
    public void Init(WagonHalfRegistry registry, TrainBuilderUI owner)
    {
        _owner = owner;
 
        var options = new List<string>();
        if (registry != null)
        {
            for (int i = 0; i < registry.Halves.Count; i++) options.Add(registry.NameOf(i));
        }
 
        _frontDropdown.ClearOptions();
        _frontDropdown.AddOptions(options);
        _backDropdown.ClearOptions();
        _backDropdown.AddOptions(options);
 
        _frontDropdown.onValueChanged.AddListener(_ => Push());
        _backDropdown.onValueChanged.AddListener(_ => Push());
        _removeButton.onClick.AddListener(() => _owner.OnRowRemoved(_index));
    }
 
    public void Show(int index, WagonSlotData data)
    {
        _index = index;
        if (_label != null) _label.text = $"Wagon {index + 1}";
 
        _frontDropdown.SetValueWithoutNotify(Mathf.Max(0, data.Front));
        _backDropdown.SetValueWithoutNotify(Mathf.Max(0, data.Back));
        _frontDropdown.RefreshShownValue();
        _backDropdown.RefreshShownValue();
    }
 
    private void Push()
    {
        _owner.OnRowChanged(_index, _frontDropdown.value, _backDropdown.value);
    }
}
