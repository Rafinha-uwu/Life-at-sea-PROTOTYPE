using UnityEngine;

public class BoatWiggle : MonoBehaviour
{
    // Roll and pitch run on their own clocks, so they drift in and out
    // of phase instead of tracing the same figure every time.
    private struct Swing
    {
        public float current;
        public float target;
        public float wait;
    }

    [Header("Roll - side to side (Z)")]
    public float minAngle = -5f;
    public float maxAngle = 5f;
    public float rotationSpeed = 2f;
    public float targetThreshold = 0.05f;
    public float pauseAtEdge = 0.2f;

    [Header("Pitch - bow up and down (X)")]
    // Tuned against the authored roll (+-8 deg at 0.7 deg/s): a shallower
    // swing on a shorter period, the way a hull actually pitches.
    public bool enablePitch = true;
    public float pitchMinAngle = -3f;
    public float pitchMaxAngle = 3f;
    public float pitchSpeed = 0.5f;
    public float pitchThreshold = 0.05f;
    public float pitchPauseAtEdge = 0.3f;

    [Header("Heave - up and down (optional)")]
    [Tooltip("Off on the ship. Switch it on for small craft that visibly ride the swell.")]
    public bool enableHeave = false;

    [Tooltip("Metres above and below the authored height.")]
    public float heaveHeight = 0.12f;

    [Tooltip("Metres per second.")]
    public float heaveSpeed = 0.08f;

    public float heavePauseAtEdge = 0.2f;

    private Quaternion restRotation;
    private Vector3 restLocalPosition;
    private Swing roll;
    private Swing pitch;
    private Swing heave;

    void Start()
    {
        // Rock around authored pose
        restRotation = transform.rotation;

        // Local, so heaving composes with anything moving the parent - the
        // ocean rising under the rowboat, for instance.
        restLocalPosition = transform.localPosition;

        roll.current = 0f;
        roll.target = Random.Range(0f, maxAngle);

        pitch.current = 0f;
        pitch.target = Random.Range(0f, pitchMaxAngle);

        heave.current = 0f;
        heave.target = Random.Range(0f, heaveHeight);
    }

    void Update()
    {
        Step(ref roll, rotationSpeed, targetThreshold, pauseAtEdge, minAngle, maxAngle);

        if (enablePitch)
            Step(ref pitch, pitchSpeed, pitchThreshold, pitchPauseAtEdge, pitchMinAngle, pitchMaxAngle);
        else
            pitch.current = Mathf.MoveTowards(pitch.current, 0f, pitchSpeed * Time.deltaTime);

        transform.rotation = restRotation * Quaternion.Euler(pitch.current, 0f, roll.current);

        if (!enableHeave) return;

        Step(ref heave, heaveSpeed, 0.001f, heavePauseAtEdge, -heaveHeight, heaveHeight);
        transform.localPosition = restLocalPosition + Vector3.up * heave.current;
    }

    private static void Step(ref Swing swing, float speed, float threshold, float pause, float min, float max)
    {
        if (swing.wait > 0f)
        {
            swing.wait -= Time.deltaTime;
            return;
        }

        swing.current = Mathf.MoveTowards(swing.current, swing.target, speed * Time.deltaTime);

        if (Mathf.Abs(swing.current - swing.target) > threshold)
            return;

        // Reached an edge: hold, then head for the other side
        swing.wait = pause;
        swing.target = swing.target > 0f
            ? Random.Range(min, 0f)
            : Random.Range(0f, max);
    }
}
