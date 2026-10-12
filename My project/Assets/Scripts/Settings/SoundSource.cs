using UnityEngine;

// Put this on anything with an AudioSource and pick its category.
// Covers children too, so one on a prefab root is usually enough.
public class SoundSource : MonoBehaviour
{
    public SoundCategory category = SoundCategory.Sfx;

    [Tooltip("Also handle AudioSources on child objects.")]
    public bool includeChildren = true;

    private AudioSource[] sources;
    private float[] authoredVolumes;

    private void Awake()
    {
        sources = includeChildren
            ? GetComponentsInChildren<AudioSource>(true)
            : GetComponents<AudioSource>();

        // Remember the mix the scene was authored with
        authoredVolumes = new float[sources.Length];
        for (int i = 0; i < sources.Length; i++)
            authoredVolumes[i] = sources[i] != null ? sources[i].volume : 1f;
    }

    private void OnEnable()
    {
        GameSettings.AudioChanged += Apply;
        Apply();
    }

    private void OnDisable()
    {
        GameSettings.AudioChanged -= Apply;
    }

    public void Apply()
    {
        if (sources == null) return;

        float level = category == SoundCategory.Music
            ? GameSettings.MusicVolume
            : GameSettings.SfxVolume;

        for (int i = 0; i < sources.Length; i++)
        {
            if (sources[i] == null) continue;

            // Mixer-routed sources are handled by AudioMixerSettings
            if (sources[i].outputAudioMixerGroup != null) continue;

            sources[i].volume = authoredVolumes[i] * level;
        }
    }
}
