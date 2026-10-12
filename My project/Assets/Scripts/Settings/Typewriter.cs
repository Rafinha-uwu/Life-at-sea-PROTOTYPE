using System.Collections;
using TMPro;
using UnityEngine;

// Reveals a TMP label character by character.
// Uses maxVisibleCharacters rather than substrings, so
// rich-text tags and word wrapping survive intact.
[RequireComponent(typeof(TMP_Text))]
public class Typewriter : MonoBehaviour
{
    [Tooltip("Characters revealed per second.")]
    public float charactersPerSecond = 28f;

    [Header("Blip")]
    public AudioSource blipSource;
    public AudioClip blip;

    [Tooltip("Several syllable sounds, one picked at random each time - gibberish speech. Overrides Blip when filled.")]
    public AudioClip[] blips;

    [Tooltip("The voice's pitch. Lower for a gruff captain, higher for a young sailor.")]
    public float basePitch = 1f;

    [Tooltip("Play a blip every N characters.")]
    public int charactersPerBlip = 2;

    [Range(0f, 0.3f)] public float blipPitchJitter = 0.08f;

    private TMP_Text label;
    private Coroutine typing;
    private int lettersSinceBlip;

    public bool IsTyping => typing != null;

    // Resolved on demand, not in Awake. Talk adds this component to speech
    // bubbles that start disabled, and AddComponent on an inactive object does
    // not run Awake - so Awake-only caching leaves this null until the bubble
    // is first shown, and Clear() on a never-shown bubble threw.
    private TMP_Text Label
    {
        get
        {
            if (label == null) label = GetComponent<TMP_Text>();
            return label;
        }
    }

    public void Show(string text)
    {
        Stop();

        Label.text = text;

        if (string.IsNullOrEmpty(text) || charactersPerSecond <= 0f)
        {
            Label.maxVisibleCharacters = int.MaxValue;
            return;
        }

        Label.maxVisibleCharacters = 0;

        // A disabled object cannot run one, and there is nothing to watch anyway
        if (!isActiveAndEnabled)
        {
            Label.maxVisibleCharacters = int.MaxValue;
            return;
        }

        typing = StartCoroutine(Reveal());
    }

    public void ShowInstant(string text)
    {
        Stop();

        Label.text = text;
        Label.maxVisibleCharacters = int.MaxValue;
    }

    // Finish the current line immediately
    public void Skip()
    {
        Stop();
        Label.maxVisibleCharacters = int.MaxValue;
    }

    public void Clear()
    {
        Stop();

        Label.text = "";
        Label.maxVisibleCharacters = int.MaxValue;
    }

    // Reveal whatever the label already holds, without touching its text.
    // The info panels get their words from the scene and from LocalizedText,
    // so the typewriter must not own the string - only how much of it shows.
    public void Replay()
    {
        Stop();

        Label.maxVisibleCharacters = 0;

        if (charactersPerSecond <= 0f || !isActiveAndEnabled)
        {
            Label.maxVisibleCharacters = int.MaxValue;
            return;
        }

        typing = StartCoroutine(Reveal());
    }

    // Hide the words without erasing them, so Replay can bring them back
    public void HideAll()
    {
        Stop();
        Label.maxVisibleCharacters = 0;
    }

    private void Stop()
    {
        if (typing != null)
        {
            StopCoroutine(typing);
            typing = null;
        }
    }

    private void OnDisable()
    {
        Stop();
    }

    private IEnumerator Reveal()
    {
        // Forces the mesh update so characterCount is right this frame
        Label.ForceMeshUpdate();

        int total = Label.textInfo.characterCount;
        float delay = 1f / charactersPerSecond;

        for (int shown = 1; shown <= total; shown++)
        {
            Label.maxVisibleCharacters = shown;

            // Only letters and numbers make a sound - spaces and punctuation are
            // the pauses that make it read as speech rather than a buzz
            char c = Label.textInfo.characterInfo[shown - 1].character;

            if (char.IsLetterOrDigit(c) && charactersPerBlip > 0 && ++lettersSinceBlip >= charactersPerBlip)
            {
                lettersSinceBlip = 0;
                PlayBlip();
            }

            yield return new WaitForSeconds(delay);
        }

        Label.maxVisibleCharacters = int.MaxValue;
        typing = null;
    }

    private void PlayBlip()
    {
        if (blipSource == null) return;

        AudioClip clip = blips != null && blips.Length > 0 ? blips[Random.Range(0, blips.Length)] : blip;
        if (clip == null) return;

        blipSource.pitch = basePitch + Random.Range(-blipPitchJitter, blipPitchJitter);
        blipSource.PlayOneShot(clip);
    }
}
