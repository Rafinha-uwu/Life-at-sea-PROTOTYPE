using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The card that pops up in front of the player when they finish a level:
// the time they just took, a star graded on that time, and a "new record"
// label when they beat their own best. GAME shows it on a win and hides it
// once the screen has gone black, so it is ready for the next level.
//
// Leave this object switched OFF in the scene. Switching it on is what plays
// its appear animation - an Animator restarts from its entry state every time
// its object is enabled - so make the appear clip the default state.
public class VictoryPanel : MonoBehaviour
{
    [Header("Bits to fill")]
    [Tooltip("The time taken this run - shown even when it is not a record.")]
    public TMP_Text timeLabel;

    [Tooltip("Shown only on a new record. Put a LocalizedText on its label for PT/EN.")]
    public GameObject newRecordLabel;

    [Tooltip("Coloured by the grade for this run's time.")]
    public Graphic star;

    public PuzzleRatings ratings;

    [Header("Sound")]
    [Tooltip("Plays as the card appears, on every win.")]
    public AudioClip victorySound;

    [Tooltip("Optional. Plays instead of the victory sound when it is a new record.")]
    public AudioClip newRecordSound;

    [Tooltip("Above 1 boosts it - go gently, past about 1.5 it can start to crackle.")]
    [Range(0f, 2f)] public float soundVolume = 1f;

    [Tooltip("Optional. The SFX group from the mixer, so the effects slider controls it.")]
    public UnityEngine.Audio.AudioMixerGroup soundGroup;

    private AudioSource audioSource;

    public void Show(int puzzle, float seconds, bool improved, bool hadPreviousRecord)
    {
        if (timeLabel != null)
            timeLabel.text = PuzzleRecords.Format(seconds, false);

        // Any time that gets stored is a record - including the first clear of
        // a level, which beats "no time at all"
        bool record = improved;

        if (newRecordLabel != null)
            newRecordLabel.SetActive(record);

        if (star != null && ratings != null)
            star.color = ratings.ColorFor(puzzle, seconds);

        // Off then on, so the appear animation replays even if it never hid
        gameObject.SetActive(false);
        gameObject.SetActive(true);

        // A PuzzleStar copied onto this card fights it: it hides the star
        // whenever ITS puzzle has no saved time, and recolours it from that
        // puzzle's best rather than this run. Silence any, then put the star
        // back exactly as this card wants it.
        foreach (PuzzleStar levelStar in GetComponentsInChildren<PuzzleStar>(true))
        {
            if (!levelStar.enabled) continue;

            Debug.LogWarning(
                $"{name}: a PuzzleStar on '{levelStar.name}' was controlling the victory star. Remove " +
                "it - the victory card colours its own star from this run's time.", levelStar);
            levelStar.enabled = false;
        }

        if (star != null)
        {
            for (Transform t = star.transform; t != null && t != transform; t = t.parent)
                t.gameObject.SetActive(true);

            star.enabled = true;
            if (ratings != null) star.color = ratings.ColorFor(puzzle, seconds);
        }

        PlaySound(record ? (newRecordSound != null ? newRecordSound : victorySound) : victorySound);

        // Straight ahead, not wherever the follower last parked the canvas
        VRMenuFollower follower = GetComponentInParent<VRMenuFollower>();
        if (follower != null) follower.SnapInFrontOfPlayer();

        if (follower != null && follower.gameObject == gameObject && GetComponent<Animator>() != null)
        {
            Debug.LogWarning(
                $"{name}: VRMenuFollower and the Animator are on the same object. The appear clip " +
                "animates this object's position, so it overwrites where the follower puts the card " +
                "every frame and the card never ends up in front of the player. Move VRMenuFollower " +
                "up to the canvas.", this);
        }
    }

    // Played after the card is switched on - an AudioSource on an inactive
    // object cannot play. 2D, so it is heard the same wherever the card sits.
    private void PlaySound(AudioClip clip)
    {
        if (clip == null) return;

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
        }

        if (soundGroup != null) audioSource.outputAudioMixerGroup = soundGroup;

        audioSource.PlayOneShot(clip, soundVolume);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }
}
