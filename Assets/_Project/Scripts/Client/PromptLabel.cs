using TMPro;
using UnityEngine;

public sealed class PromptLabel : MonoBehaviour
{
    public static TMP_Text Instance { get; private set; }
    private void Awake() { Instance = GetComponent<TMP_Text>(); }
    private void OnDestroy() { if (Instance == GetComponent<TMP_Text>()) Instance = null; }
}