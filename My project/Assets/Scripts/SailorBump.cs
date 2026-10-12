using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

// Goes on a sailor, with a trigger collider around him. Reacts to the player
// walking into him: a grumble, optionally a shove, optionally time off the
// spice clock.
//
// A trigger rather than a solid collider on purpose - the player moves with a
// CharacterController, which pushes through non-trigger colliders without ever
// reporting anything to the thing it hit.
[RequireComponent(typeof(Collider))]
public class SailorBump : MonoBehaviour
{
    [Header("Who counts as the player")]
    [Tooltip("Anything with a CharacterController counts. Set a tag as well to be stricter.")]
    public string playerTag = "";

    [Header("Complaint")]
    public AudioSource voice;

    [Tooltip("One is picked at random. Leave empty to play whatever the AudioSource already holds.")]
    public AudioClip[] grumbles;

    [Tooltip("Seconds before he will complain again, so brushing past is not a machine gun.")]
    public float cooldown = 2f;

    [Header("Shove")]
    [Tooltip("Optional. Assign his Rigidbody to let the player knock him over - off the rail, for instance.")]
    public Rigidbody body;

    [Tooltip("His Animator. Assigning it is what makes this sailor fall over when bumped.")]
    [FormerlySerializedAs("animatorToStop")]
    public Animator animator;

    [Tooltip("Animator trigger that plays the fall. Case-sensitive. Leave empty to switch the Animator off instead and let physics alone knock him over.")]
    public string fallTrigger = "fall";

    public float shoveForce = 4f;

    [Header("Spice game penalty")]
    [Tooltip("Optional. Bumping costs this much time while an L4 round is running.")]
    public VRCollectMinigame spiceGame;

    public float secondsLost = 3f;

    [Header("Events")]
    public UnityEvent onBumped;

    private float nextAllowed;
    private bool hasFallen;

    // Every sailor, so GAME can stand them all back up in one call
    private static readonly List<SailorBump> All = new();

    private struct Pose
    {
        public Transform transform;
        public Vector3 localPosition;
        public Quaternion localRotation;
    }

    // Local, because he rides the rocking ship
    private readonly List<Pose> rest = new();
    private bool bodyWasKinematic;

    private void Awake()
    {
        All.Add(this);

        Remember(transform);

        if (body != null)
        {
            Remember(body.transform);
            bodyWasKinematic = body.isKinematic;
        }

        // Root motion in the fall clip moves this one
        if (animator != null) Remember(animator.transform);
    }

    private void OnDestroy()
    {
        All.Remove(this);
    }

    private void Remember(Transform t)
    {
        foreach (Pose pose in rest)
        {
            if (pose.transform == t) return;
        }

        rest.Add(new Pose { transform = t, localPosition = t.localPosition, localRotation = t.localRotation });
    }

    // Called by GAME on the way back to the rowboat, so the next visit has
    // everyone standing where they started, ready to go over the side again
    public static void ResetAll()
    {
        for (int i = All.Count - 1; i >= 0; i--)
        {
            if (All[i] != null) All[i].ResetSailor();
        }
    }

    public void ResetSailor()
    {
        if (!hasFallen) return;

        hasFallen = false;

        // Stop him first - velocity cannot be set on a kinematic body
        if (body != null)
        {
            if (!body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }

            body.isKinematic = bodyWasKinematic;
        }

        foreach (Pose pose in rest)
        {
            if (pose.transform == null) continue;

            pose.transform.localPosition = pose.localPosition;
            pose.transform.localRotation = pose.localRotation;
        }

        if (animator != null)
        {
            animator.enabled = true;

            // Back to the entry state with the trigger cleared. An inactive
            // Animator resets itself when next enabled, so only rebind live ones
            if (animator.isActiveAndEnabled)
            {
                animator.Rebind();
                animator.Update(0f);
            }
        }
    }

    // Statics survive "enter play mode without domain reload", so wipe them
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        All.Clear();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (Time.time < nextAllowed) return;
        if (!IsPlayer(other)) return;

        nextAllowed = Time.time + cooldown;

        Complain();
        Shove(other.transform.position);

        if (spiceGame != null)
            spiceGame.LoseTime(secondsLost);

        onBumped.Invoke();
    }

    private bool IsPlayer(Collider other)
    {
        if (other == null) return false;

        if (!string.IsNullOrEmpty(playerTag) && !other.transform.root.CompareTag(playerTag))
            return false;

        // The rig walks on a CharacterController, so that is the reliable tell
        return other is CharacterController
            || other.GetComponentInParent<CharacterController>() != null;
    }

    private void Complain()
    {
        if (voice == null) return;

        if (grumbles != null && grumbles.Length > 0)
        {
            AudioClip clip = grumbles[Random.Range(0, grumbles.Length)];
            if (clip != null) voice.PlayOneShot(clip);
            return;
        }

        voice.Play();
    }

    private void Shove(Vector3 from)
    {
        // Once over, he stays over - a second bump must not replay the fall
        if (hasFallen) return;
        if (animator == null && body == null) return;

        hasFallen = true;

        if (animator != null)
        {
            if (!string.IsNullOrEmpty(fallTrigger))
                animator.SetTrigger(fallTrigger);
            else
                // No fall clip: let go of the animation entirely, or it keeps
                // writing the bones over whatever physics does and he pops upright
                animator.enabled = false;
        }

        if (body == null) return;

        body.isKinematic = false;

        Vector3 away = transform.position - from;
        away.y = 0f;

        if (away.sqrMagnitude < 0.0001f) away = transform.forward;

        body.AddForce(away.normalized * shoveForce, ForceMode.VelocityChange);
    }
}
