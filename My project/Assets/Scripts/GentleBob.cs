using UnityEngine;

// Slow vertical drift, for feel. Works on UI and on anything else.
//
// It adds its offset on top of whatever the position already is rather than
// owning the position outright: each frame it takes its own last offset back
// off first, so an Animator driving the same object still gets its way. That
// matters on the info panels, whose open/close clips animate the very same
// anchored position this moves.
public class GentleBob : MonoBehaviour
{
    [Tooltip("What to move. Leave empty to move this object.")]
    public Transform target;

    [Tooltip("How far it travels each way. UI sits in small numbers - start around 0.02 for a world-space panel.")]
    public float distance = 0.02f;

    [Tooltip("Seconds for one full down-up-down. Bigger is slower and calmer.")]
    public float secondsPerBounce = 4f;

    [Tooltip("Start somewhere random in the cycle, so several of these do not move as one.")]
    public bool randomStartPhase = true;

    [Tooltip("Keeps going while the game is paused. Usually what you want for menus.")]
    public bool useUnscaledTime = true;

    [Tooltip("Bob along world up instead of the object's own up. Use it if the object is tilted but should still rise straight.")]
    public bool useWorldUp = false;

    private Transform Target => target != null ? target : transform;

    private Vector3 appliedOffset;
    private float phase;

    private void OnEnable()
    {
        phase = randomStartPhase ? Random.value * Mathf.PI * 2f : 0f;
        appliedOffset = Vector3.zero;
    }

    private void OnDisable()
    {
        // Hand the position back exactly as it was found
        if (appliedOffset != Vector3.zero)
        {
            Target.localPosition -= appliedOffset;
            appliedOffset = Vector3.zero;
        }
    }

    // After the Animator, so this is layered on the animated value
    private void LateUpdate()
    {
        if (secondsPerBounce <= 0f) return;

        Transform moved = Target;

        float time = useUnscaledTime ? Time.unscaledTime : Time.time;
        float t = time / secondsPerBounce * Mathf.PI * 2f + phase;

        Vector3 up = Vector3.up;

        if (useWorldUp && moved.parent != null)
            up = moved.parent.InverseTransformDirection(Vector3.up);

        Vector3 offset = up * (Mathf.Sin(t) * distance);

        // Take last frame's contribution back before adding this one, so
        // whatever else wrote to the position in between survives
        moved.localPosition += offset - appliedOffset;
        appliedOffset = offset;
    }
}
