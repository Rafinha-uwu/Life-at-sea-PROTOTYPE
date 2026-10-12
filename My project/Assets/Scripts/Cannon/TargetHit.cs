using System.Collections;
using UnityEngine;

// A cannon target. When a ball hits it, it plays its explode animation (if it
// has one), then hides and counts towards the level.
//
// The animation: give the target (or a child) an Animator whose controller
// has a default resting state plus a state called "Explode" with your clip.
// No transitions or parameters needed - this jumps straight to the state.
// The target hides when the clip ends, and is put back to its resting pose
// the next time L3 starts.
public class TargetHit : MonoBehaviour
{
    public GAME manager;

    [Header("Effects")]
    public GameObject hitEffectPrefab;

    [Tooltip("How far above the target's centre the hit effect appears, in metres. Measured along the ship's up, so it stays straight while the deck rocks.")]
    public float hitEffectHeight = 0.5f;

    [Header("Explode Animation")]
    [Tooltip("Leave empty to use the first Animator on this object or its children. None at all: hides instantly, as before.")]
    public Animator explodeAnimator;

    [Tooltip("The state name in the Animator Controller, exactly as written there.")]
    public string explodeState = "Explode";

    [Tooltip("Seconds to wait before hiding. 0 = the length of the explode clip.")]
    public float hideAfter = 0f;

    private Collider[] colliders;
    private bool hit;

    private void Awake()
    {
        colliders = GetComponentsInChildren<Collider>(true);

        if (explodeAnimator == null)
            explodeAnimator = GetComponentInChildren<Animator>(true);
    }

    // GAME switches the targets back on for each run - undo the explosion
    private void OnEnable()
    {
        hit = false;

        foreach (Collider part in colliders)
            if (part != null) part.enabled = true;

        if (explodeAnimator != null && explodeAnimator.runtimeAnimatorController != null)
        {
            // Back to the resting state and every animated value back to default
            explodeAnimator.Rebind();
            explodeAnimator.Update(0f);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (hit || !collision.gameObject.CompareTag("CannonProjectile"))
            return;

        // Once only - a second ball, or the same one bouncing, must not count again
        hit = true;

        if (hitEffectPrefab != null)
        {
            Vector3 up = transform.parent != null ? transform.parent.up : Vector3.up;
            GameObject fx = Instantiate(hitEffectPrefab, transform.position + up * hitEffectHeight, transform.rotation);
            ShipRoot.AttachBeside(fx, transform);
        }

        StartCoroutine(Explode());
    }

    private IEnumerator Explode()
    {
        // Balls pass through the wreck while it plays
        foreach (Collider part in colliders)
            if (part != null) part.enabled = false;

        if (HasExplodeState())
        {
            int state = Animator.StringToHash(explodeState);

            // Explicit time, or Play does nothing if it is already in that state
            explodeAnimator.Play(state, 0, 0f);

            float wait = hideAfter;

            if (wait <= 0f)
            {
                // The state's length is only known once it is actually playing
                yield return null;
                AnimatorStateInfo info = explodeAnimator.GetCurrentAnimatorStateInfo(0);
                wait = info.shortNameHash == state ? info.length - Time.deltaTime : 0f;
            }

            if (wait > 0f) yield return new WaitForSeconds(wait);
        }
        else if (hideAfter > 0f)
        {
            yield return new WaitForSeconds(hideAfter);
        }

        // Hide so L3 replays
        gameObject.SetActive(false);

        // Counted after the explosion, so the last target is seen to blow up
        // before the level ends
        if (manager != null)
            manager.TargetDestroyed();
    }

    private bool HasExplodeState()
    {
        if (explodeAnimator == null || explodeAnimator.runtimeAnimatorController == null || string.IsNullOrEmpty(explodeState))
            return false;

        if (explodeAnimator.HasState(0, Animator.StringToHash(explodeState)))
            return true;

        Debug.LogWarning($"{name}: the Animator has no state called \"{explodeState}\" on its base layer - hiding without the animation.", this);
        return false;
    }
}
