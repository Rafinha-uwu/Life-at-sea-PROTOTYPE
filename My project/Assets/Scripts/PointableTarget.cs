using UnityEngine;
using UnityEngine.Events;

// A bird or island to spot from the crow's nest. Point at it for
// requiredPointTime and the level is done.
//
// Counts pointers rather than assuming one: with a pointer on each hand, both
// can be on the same target at once. Without counting, two hands would tick
// the timer twice as fast, and one hand looking away would reset it while the
// other was still holding steady.
public class PointableTarget : MonoBehaviour
{
    [SerializeField] private float requiredPointTime = 3f;

    public GAME game;

    [SerializeField] private UnityEvent onPointCompleted;

    private int pointers;
    private bool completed;
    private float pointTimer;
    private int lastTickFrame = -1;

    private void OnEnable()
    {
        completed = false;
        pointers = 0;
        pointTimer = 0f;
    }

    public void OnPointEnter(RaycastHit hit)
    {
        pointers++;

        // The first hand on it starts the clock; a second one joining does not
        if (pointers == 1) pointTimer = 0f;
    }

    public void OnPointStay(RaycastHit hit)
    {
        if (pointers == 0 || completed)
            return;

        // Both hands call this every frame - only count the frame once
        if (Time.frameCount == lastTickFrame)
            return;

        lastTickFrame = Time.frameCount;
        pointTimer += Time.deltaTime;

        if (pointTimer < requiredPointTime)
            return;

        completed = true;

        // Every hand, not just the one that finished it
        PointerReader.SetAllPointingEnabled(false);

        if (game != null)
            game.Hub();

        onPointCompleted?.Invoke();
    }

    public void OnPointExit()
    {
        pointers = Mathf.Max(0, pointers - 1);

        // Still held by the other hand - keep the progress
        if (pointers > 0) return;

        pointTimer = 0f;
    }

    public float Progress01 =>
        requiredPointTime <= 0f ? 1f : Mathf.Clamp01(pointTimer / requiredPointTime);
}
