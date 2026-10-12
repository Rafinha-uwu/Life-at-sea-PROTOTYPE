using System.Collections;
using UnityEngine;
using UnityEngine.Audio;

// One main theme for the start menu and exploring, one track per level, and a
// crossfade between them. GAME tells it which to play; it switches while the
// screen is black, so the change is heard but never seen.
//
// Volume comes from the Music mixer group, which AudioMixerSettings drives
// from the options slider - so do NOT put a SoundSource on this object as
// well, or the two will both scale the volume.
public class MusicDirector : MonoBehaviour
{
    [Header("Tracks")]
    [Tooltip("Start menu and exploring.")]
    public AudioClip mainTheme;

    [Tooltip("Element 0 is L1 (mast), 1 is L2 (stars), 2 is L3 (cannon), 3 is L4 (cargo). An empty slot keeps the main theme playing for that level.")]
    public AudioClip[] levelTracks = new AudioClip[4];

    [Header("Output")]
    [Tooltip("The Music group on your mixer, so the settings slider controls it.")]
    public AudioMixerGroup musicGroup;

    [Range(0f, 1f)] public float volume = 0.8f;

    [Tooltip("Seconds to crossfade between tracks.")]
    public float crossfadeTime = 1.5f;

    public bool playMainOnStart = true;

    [Header("Quieter up the mast")]
    [Tooltip("An empty at deck level, under ALL - the same one the waves' HeightFade uses. Empty: music never changes with height.")]
    public Transform deckLevel;

    [Tooltip("Metres above the deck where the music starts to drop.")]
    public float fadeStartsAbove = 2f;

    [Tooltip("Metres above the deck where it is at its quietest.")]
    public float quietestAbove = 30f;

    [Tooltip("Music volume at the top, as a fraction. Keep it gentle - the wind and waves do the heavy lifting.")]
    [Range(0f, 1f)] public float musicAtTop = 0.75f;

    [Header("Ambience")]
    [Tooltip("Loops under the music for a level - the storm on the cannon level. Same slot order as Level Tracks. Empty slot = no ambience for that level.")]
    public AudioClip[] levelAmbience = new AudioClip[4];

    [Tooltip("Mixer group for ambience. The SFX group makes it follow the effects slider rather than the music one. Empty = the music group.")]
    public AudioMixerGroup ambienceGroup;

    [Range(0f, 1f)] public float ambienceVolume = 0.7f;

    private AudioSource ambience;
    private Coroutine ambienceFade;

    private AudioSource[] sources;
    private int current;

    // Each track's crossfade level. The real volume is this times the height
    // fade, applied in one place, so the two never fight over AudioSource.volume
    private readonly float[] level = new float[2];
    private float heightFade = 1f;
    private Transform listener;
    private Coroutine fading;

    private void Awake()
    {
        // Two sources, so the outgoing track can fade while the next one rises
        sources = new AudioSource[2];

        for (int i = 0; i < sources.Length; i++)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = 0f;
            source.outputAudioMixerGroup = musicGroup;
            sources[i] = source;
        }

        // Ambience is a separate layer, so it runs alongside the music
        // instead of being crossfaded against it
        ambience = gameObject.AddComponent<AudioSource>();
        ambience.loop = true;
        ambience.playOnAwake = false;
        ambience.spatialBlend = 0f;
        ambience.volume = 0f;
        ambience.outputAudioMixerGroup = ambienceGroup != null ? ambienceGroup : musicGroup;
    }

    // The one place music volume is written: crossfade level times height fade
    private void Update()
    {
        float target = 1f;

        if (deckLevel != null)
        {
            if (listener == null && Camera.main != null) listener = Camera.main.transform;

            if (listener != null)
                target = Mathf.Lerp(1f, musicAtTop,
                    HeightFade.Height01(deckLevel, listener, fadeStartsAbove, quietestAbove));
        }

        heightFade = Mathf.Lerp(heightFade, target, 1f - Mathf.Exp(-3f * Time.unscaledDeltaTime));

        for (int i = 0; i < sources.Length; i++)
            sources[i].volume = level[i] * heightFade;
    }

    private void Start()
    {
        if (playMainOnStart) PlayMain();
    }

    public void PlayMain()
    {
        PlayClip(mainTheme);
        SetAmbience(null);
    }

    // Level numbers are 1-4, like everywhere else in GAME
    public void PlayLevel(int level)
    {
        int index = level - 1;

        AudioClip clip = levelTracks != null && index >= 0 && index < levelTracks.Length
            ? levelTracks[index]
            : null;

        PlayClip(clip != null ? clip : mainTheme);

        AudioClip layer = levelAmbience != null && index >= 0 && index < levelAmbience.Length
            ? levelAmbience[index]
            : null;

        SetAmbience(layer);
    }

    private void SetAmbience(AudioClip clip)
    {
        if (ambienceFade != null) StopCoroutine(ambienceFade);

        if (clip == null)
        {
            if (ambience.isPlaying)
                ambienceFade = StartCoroutine(FadeAmbience(0f, true));
            return;
        }

        // Same storm still fading out from the last run - just bring it back
        // up rather than restarting it from the top
        if (ambience.clip != clip || !ambience.isPlaying)
        {
            ambience.clip = clip;
            ambience.volume = 0f;
            ambience.Play();
        }

        ambienceFade = StartCoroutine(FadeAmbience(ambienceVolume, false));
    }

    private IEnumerator FadeAmbience(float target, bool stopAfter)
    {
        float start = ambience.volume;
        float elapsed = 0f;

        while (elapsed < crossfadeTime)
        {
            elapsed += Time.unscaledDeltaTime;
            ambience.volume = Mathf.Lerp(start, target, crossfadeTime > 0f ? elapsed / crossfadeTime : 1f);
            yield return null;
        }

        ambience.volume = target;
        if (stopAfter) ambience.Stop();

        ambienceFade = null;
    }

    private void PlayClip(AudioClip clip)
    {
        if (clip == null) return;

        // Already playing it - carry on rather than restart from the top
        if (sources[current].clip == clip && sources[current].isPlaying) return;

        int next = 1 - current;

        sources[next].clip = clip;
        level[next] = 0f;
        sources[next].Play();

        if (fading != null) StopCoroutine(fading);
        fading = StartCoroutine(Crossfade(current, next));

        current = next;
    }

    // Unscaled, so opening the pause menu mid-fade does not freeze it
    private IEnumerator Crossfade(int from, int to)
    {
        float startFrom = level[from];
        float elapsed = 0f;

        while (elapsed < crossfadeTime)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = crossfadeTime > 0f ? elapsed / crossfadeTime : 1f;

            level[from] = Mathf.Lerp(startFrom, 0f, t);
            level[to] = Mathf.Lerp(0f, volume, t);

            yield return null;
        }

        level[from] = 0f;
        sources[from].Stop();
        level[to] = volume;

        fading = null;
    }
}
