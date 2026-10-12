using TMPro;
using UnityEngine;

// Two strings on a TMP label, swapped by the language setting.
[RequireComponent(typeof(TMP_Text))]
public class LocalizedText : MonoBehaviour
{
    [TextArea] public string portuguese;
    [TextArea] public string english;

    private TMP_Text label;

    private void Awake()
    {
        label = GetComponent<TMP_Text>();

        // Seed from whatever was typed in the scene
        if (string.IsNullOrEmpty(portuguese) && string.IsNullOrEmpty(english))
            portuguese = label.text;
    }

    private void OnEnable()
    {
        GameSettings.LanguageChanged += Apply;
        Apply();
    }

    private void OnDisable()
    {
        GameSettings.LanguageChanged -= Apply;
    }

    private void Apply()
    {
        if (label == null) return;

        string text = GameSettings.Pick(portuguese, english);

        if (!string.IsNullOrEmpty(text))
            label.text = text;
    }
}
