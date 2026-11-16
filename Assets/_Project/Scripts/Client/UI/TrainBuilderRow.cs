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

    [Tooltip("Optional. Upgrades the wagon once for more HP. Hidden while building is free (lobby).")]
    [SerializeField] private Button _upgradeButton;
    [SerializeField] private TMP_Text _upgradeLabel;

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

        if (_upgradeButton != null)
        {
            _upgradeButton.onClick.AddListener(OnUpgradeClicked);
        }
    }

    public void Show(int index, WagonSlotData data, bool upgradesAvailable, bool canAffordUpgrade)
    {
        _index = index;
        if (_label != null)
        {
            string label = "Wagon " + (index + 1);
            if (data.Upgraded)
            {
                label += " (upgraded)";
            }

            _label.text = label;
        }

        ShowUpgrade(data.Upgraded, upgradesAvailable, canAffordUpgrade);
 
        _frontDropdown.SetValueWithoutNotify(Mathf.Max(0, data.Front));
        _backDropdown.SetValueWithoutNotify(Mathf.Max(0, data.Back));
        _frontDropdown.RefreshShownValue();
        _backDropdown.RefreshShownValue();
    }
 
    private void Push()
    {
        _owner.OnRowChanged(_index, _frontDropdown.value, _backDropdown.value);
    }

    private void OnUpgradeClicked()
    {
        _owner.OnRowUpgraded(_index);
    }

    private void ShowUpgrade(bool upgraded, bool upgradesAvailable, bool canAffordUpgrade)
    {
        if (_upgradeButton == null)
        {
            return;
        }

        _upgradeButton.gameObject.SetActive(upgradesAvailable);
        _upgradeButton.interactable = !upgraded && canAffordUpgrade;

        if (_upgradeLabel == null)
        {
            return;
        }

        if (upgraded)
        {
            _upgradeLabel.text = "Upgraded";
        }
        else
        {
            _upgradeLabel.text = "Upgrade";
        }
    }
}
