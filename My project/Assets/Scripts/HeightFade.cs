using UnityEngine;

// Fades a sound by how high the player's head is above the deck - the waves
// full on deck and dying away up the mast. Height only: walking the length of
// the ship changes nothing, which a plain 3D sound could not do (it fades with
// distance in every direction).
//
// Height is measured in the deck marker's own space, so the ship rocking does
// not make the volume swell and dip.
[RequireComponent(typeof(AudioSource))]
public class HeightFade : MonoBehaviour
{
    [Tooltip("An empty at deck level, under ALL so it rides the ship.")]
    public Transform deckLevel;

    [Tooltip("Leave empty to use the main camera.")]
    public Transform player;

    [Tooltip("Metres above the deck where the fade starts - stays full below this.")]
    public float fadeStartsAbove = 2f;

    [Tooltip("Metres above the deck where it reaches its quietest.")]
    public float quietestAbove = 30f;

    [Tooltip("Volume at the top, as a fraction of the normal volume. 0 is silent.")]
    [Range(0f, 1f)] public float quietestVolume = 0.15f;

    [Tooltip("Flip it: quiet on deck, loud up high. For wind at the top of the mast.")]
    public bool louderWithHeight = false;

    [Tooltip("How quickly it follows, so a fall or a teleport does not snap the volume.")]
    public float smoothing = 3f;

    private AudioSource source;
    private float authoredVolume;
    private float current = 1f;
    private bool started;

    private void Awake()
    {
        source = GetComponent<AudioSource>();

        // The volume set in the inspector is the "full" level; this only
        // scales it down. The settings slider works through the mixer group.
        authoredVolume = source.volume;
    }

    private void OnEnable()
    {
        if (player == null && Camera.main != null)
            player = Camera.main.transform;
    }

    // 0 on the deck, 1 at the top, eased so the middle of the climb changes
    // gradually. Measured in the deck marker's space so the rocking is ignored.
    // Shared with MusicDirector, so the music and the waves agree on "height".
    public static float Height01(Transform deck, Transform head, float start, float end)
    {
        float height = deck.InverseTransformPoint(head.position).y;
        return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(start, end, height));
    }

    private void Update()
    {
        if (deckLevel == null || player == null) return;

        float t = Height01(deckLevel, player, fadeStartsAbove, quietestAbove);
        if (louderWithHeight) t = 1f - t;

        float target = Mathf.Lerp(1f, quietestVolume, t);

        // First frame snaps, so the wind does not blast for a second on load
        // while it eases down to "on deck"
        if (!started) { current = target; started = true; }

        current = Mathf.Lerp(current, target, 1f - Mathf.Exp(-smoothing * Time.deltaTime));
        source.volume = authoredVolume * current;
    }
}
