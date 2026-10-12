using UnityEngine;
using UnityEngine.Audio;

// Drives an AudioMixer from GameSettings. One of these in the scene
// covers every source routed to the Music/SFX groups.
public class AudioMixerSettings : MonoBehaviour
{
    [Header("Mixer")]
    public AudioMixer mixer;

    [Tooltip("Exposed parameter names on the mixer.")]
    public string musicParameter = "MusicVolume";
    public string sfxParameter = "SfxVolume";

    [Header("Range")]
    [Tooltip("Volume in dB at slider 0. Below -80 is silence.")]
    public float minDecibels = -80f;
    public float maxDecibels = 0f;

    private void OnEnable()
    {
        GameSettings.AudioChanged += Apply;
        Apply();
    }

    private void OnDisable()
    {
        GameSettings.AudioChanged -= Apply;
    }

    private bool warned;

    private void Apply()
    {
        if (mixer == null)
        {
            WarnOnce("AudioMixerSettings has no mixer assigned.");
            return;
        }

        bool musicOk = mixer.SetFloat(musicParameter, ToDecibels(GameSettings.MusicVolume));
        bool sfxOk = mixer.SetFloat(sfxParameter, ToDecibels(GameSettings.SfxVolume));

        // SetFloat fails silently on a wrong name, which is a horrible thing to debug
        if (!musicOk)
            WarnOnce($"Mixer has no exposed parameter called \"{musicParameter}\".");

        if (!sfxOk)
            WarnOnce($"Mixer has no exposed parameter called \"{sfxParameter}\".");
    }

    private void WarnOnce(string message)
    {
        if (warned) return;

        warned = true;
        Debug.LogError(message + " Check the exposed parameters on the mixer asset (right-click a volume > Expose).", this);
    }

    // Sliders are linear, mixers are logarithmic - a straight
    // mapping makes the top half of the slider do almost nothing
    private float ToDecibels(float linear)
    {
        if (linear <= 0.0001f) return -80f;

        return Mathf.Clamp(Mathf.Log10(linear) * 20f, minDecibels, maxDecibels);
    }
}
