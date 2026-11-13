using UnityEngine;

[CreateAssetMenu(menuName = "Rewards/Medal Definition")]
public class MedalDefinition : ScriptableObject
{
    [SerializeField] private string id;
    [SerializeField] private string displayName;
    [SerializeField] private Sprite icon;

    public string Id
    {
        get { return id; }
    }

    public string DisplayName
    {
        get { return displayName; }
    }

    public Sprite Icon
    {
        get { return icon; }
    }
}
