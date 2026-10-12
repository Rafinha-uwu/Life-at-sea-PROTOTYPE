using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Wires the options page to GameSettings. Every field is optional,
// so the page can be built up a control at a time.
public class SettingsMenu : MonoBehaviour
{
    [Header("Audio")]
    public Slider musicSlider;
    public Slider sfxSlider;
    public TMP_Text musicValueLabel;
    public TMP_Text sfxValueLabel;

    [Header("Player Height")]
    public Slider heightSlider;
    public TMP_Text heightValueLabel;

    [Header("Text Size")]
    public Slider textScaleSlider;
    public TMP_Text textScaleValueLabel;

    [Header("Language")]
    [Tooltip("Two options, in this order: Portugues, English.")]
    public TMP_Dropdown languageDropdown;

    private bool loading;

    private void OnEnable()
    {
        LoadIntoControls();

        if (musicSlider != null) musicSlider.onValueChanged.AddListener(OnMusic);
        if (sfxSlider != null) sfxSlider.onValueChanged.AddListener(OnSfx);
        if (heightSlider != null) heightSlider.onValueChanged.AddListener(OnHeight);
        if (textScaleSlider != null) textScaleSlider.onValueChanged.AddListener(OnTextScale);
        if (languageDropdown != null) languageDropdown.onValueChanged.AddListener(OnLanguage);
    }

    private void OnDisable()
    {
        if (musicSlider != null) musicSlider.onValueChanged.RemoveListener(OnMusic);
        if (sfxSlider != null) sfxSlider.onValueChanged.RemoveListener(OnSfx);
        if (heightSlider != null) heightSlider.onValueChanged.RemoveListener(OnHeight);
        if (textScaleSlider != null) textScaleSlider.onValueChanged.RemoveListener(OnTextScale);
        if (languageDropdown != null) languageDropdown.onValueChanged.RemoveListener(OnLanguage);

        GameSettings.Save();
    }

    // Pushing saved values into sliders fires their callbacks, so guard it
    private void LoadIntoControls()
    {
        loading = true;

        if (musicSlider != null)
        {
            musicSlider.minValue = 0f;
            musicSlider.maxValue = 1f;
            musicSlider.value = GameSettings.MusicVolume;
        }

        if (sfxSlider != null)
        {
            sfxSlider.minValue = 0f;
            sfxSlider.maxValue = 1f;
            sfxSlider.value = GameSettings.SfxVolume;
        }

        if (heightSlider != null)
        {
            heightSlider.minValue = -0.6f;
            heightSlider.maxValue = 0.6f;
            heightSlider.value = GameSettings.HeightOffset;
        }

        if (textScaleSlider != null)
        {
            textScaleSlider.minValue = 0.6f;
            textScaleSlider.maxValue = 1.5f;
            textScaleSlider.value = GameSettings.TextScale;
        }

        if (languageDropdown != null)
        {
            // Built here so the labels and the enum order can never disagree
            languageDropdown.ClearOptions();
            languageDropdown.AddOptions(new List<string> { "Português", "English" });
            languageDropdown.SetValueWithoutNotify((int)GameSettings.Language);
            languageDropdown.RefreshShownValue();
        }

        loading = false;
        RefreshLabels();
    }

    private void OnMusic(float value)
    {
        if (loading) return;
        GameSettings.MusicVolume = value;
        RefreshLabels();
    }

    private void OnSfx(float value)
    {
        if (loading) return;
        GameSettings.SfxVolume = value;
        RefreshLabels();
    }

    private void OnHeight(float value)
    {
        if (loading) return;
        GameSettings.HeightOffset = value;
        RefreshLabels();
    }

    private void OnTextScale(float value)
    {
        if (loading) return;
        GameSettings.TextScale = value;
        RefreshLabels();
    }

    private void OnLanguage(int value)
    {
        if (loading) return;
        GameSettings.Language = (GameLanguage)value;
    }

    public void ResetToDefaults()
    {
        GameSettings.MusicVolume = 0.8f;
        GameSettings.SfxVolume = 1f;
        GameSettings.HeightOffset = 0f;
        GameSettings.TextScale = 1f;

        LoadIntoControls();
    }

    private void RefreshLabels()
    {
        if (musicValueLabel != null)
            musicValueLabel.text = Mathf.RoundToInt(GameSettings.MusicVolume * 100f) + "%";

        if (sfxValueLabel != null)
            sfxValueLabel.text = Mathf.RoundToInt(GameSettings.SfxVolume * 100f) + "%";

        if (heightValueLabel != null)
        {
            float cm = GameSettings.HeightOffset * 100f;
            heightValueLabel.text = (cm >= 0f ? "+" : "") + Mathf.RoundToInt(cm) + " cm";
        }

        if (textScaleValueLabel != null)
            textScaleValueLabel.text = Mathf.RoundToInt(GameSettings.TextScale * 100f) + "%";
    }
}
