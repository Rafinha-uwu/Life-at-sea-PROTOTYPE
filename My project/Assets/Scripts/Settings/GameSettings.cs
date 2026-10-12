using System;
using UnityEngine;

public enum GameLanguage
{
    Portuguese = 0,
    English = 1,
}

public enum SoundCategory
{
    Music = 0,
    Sfx = 1,
}

// Values live in PlayerPrefs; everything else listens for changes.
public static class GameSettings
{
    private const string MusicKey = "nau.music";
    private const string SfxKey = "nau.sfx";
    private const string HeightKey = "nau.height";
    private const string TextScaleKey = "nau.textScale";
    private const string LanguageKey = "nau.language";

    public static event Action AudioChanged;
    public static event Action HeightChanged;
    public static event Action TextScaleChanged;
    public static event Action LanguageChanged;

    private static float musicVolume = -1f;
    private static float sfxVolume = -1f;
    private static float heightOffset = float.NaN;
    private static float textScale = -1f;
    private static int language = -1;

    public static float MusicVolume
    {
        get
        {
            if (musicVolume < 0f) musicVolume = PlayerPrefs.GetFloat(MusicKey, 0.8f);
            return musicVolume;
        }
        set
        {
            musicVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(MusicKey, musicVolume);
            AudioChanged?.Invoke();
        }
    }

    public static float SfxVolume
    {
        get
        {
            if (sfxVolume < 0f) sfxVolume = PlayerPrefs.GetFloat(SfxKey, 1f);
            return sfxVolume;
        }
        set
        {
            sfxVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(SfxKey, sfxVolume);
            AudioChanged?.Invoke();
        }
    }

    // Metres added to the player's eye height
    public static float HeightOffset
    {
        get
        {
            if (float.IsNaN(heightOffset)) heightOffset = PlayerPrefs.GetFloat(HeightKey, 0f);
            return heightOffset;
        }
        set
        {
            heightOffset = Mathf.Clamp(value, -0.6f, 0.6f);
            PlayerPrefs.SetFloat(HeightKey, heightOffset);
            HeightChanged?.Invoke();
        }
    }

    public static float TextScale
    {
        get
        {
            if (textScale < 0f) textScale = PlayerPrefs.GetFloat(TextScaleKey, 1f);
            return textScale;
        }
        set
        {
            textScale = Mathf.Clamp(value, 0.6f, 2.5f);
            PlayerPrefs.SetFloat(TextScaleKey, textScale);
            TextScaleChanged?.Invoke();
        }
    }

    public static GameLanguage Language
    {
        get
        {
            if (language < 0) language = PlayerPrefs.GetInt(LanguageKey, (int)GameLanguage.Portuguese);
            return (GameLanguage)language;
        }
        set
        {
            language = (int)value;
            PlayerPrefs.SetInt(LanguageKey, language);
            LanguageChanged?.Invoke();
        }
    }

    public static string Pick(string portuguese, string english)
    {
        return Language == GameLanguage.English ? english : portuguese;
    }

    public static void Save()
    {
        PlayerPrefs.Save();
    }

    // Statics survive "enter play mode without domain reload", so wipe them
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        AudioChanged = null;
        HeightChanged = null;
        TextScaleChanged = null;
        LanguageChanged = null;

        musicVolume = -1f;
        sfxVolume = -1f;
        heightOffset = float.NaN;
        textScale = -1f;
        language = -1;
    }
}
