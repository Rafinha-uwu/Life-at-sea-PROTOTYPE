using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Climbing;

// Catches the player falling off the mast or over the side and puts them back.
//
// Detection is deliberately not "did they land": falling into the sea never
// lands, so waiting for ground meant the worst falls were the ones that did
// nothing. It now fires as soon as they are far enough below the highest point
// they reached, in the air, and has a second net for going below the deck.
[RequireComponent(typeof(CharacterController))]
public class Fall : MonoBehaviour
{
    [Header("Fall Settings")]
    public float minimumFallHeight = 2f;
    public bool isClimbing;

    [Tooltip("Also respawn once this far below the respawn point, for falls that never land - over the side, into the sea.")]
    public float limitBelowRespawn = 8f;

    [Tooltip("How long grounding is remembered. The deck rocks and CharacterController.isGrounded flickers on it, which used to wipe the fall mid-drop.")]
    public float groundedGrace = 0.2f;

    [Tooltip("Quiet time after a respawn, so arriving cannot immediately count as another fall.")]
    public float respawnCooldown = 1f;

    [Tooltip("Switch the CharacterController off while hanging from the handles, so the body does not snag on rungs and the nest rim on the way up. XRI moves the rig directly while it is off - that is a supported path, not a hack.")]
    public bool passThroughWhileClimbing = true;

    [Header("References")]
    public GameObject Fade;

    [FormerlySerializedAs("Target1")]
    public GameObject respawnPoint;

    private float highestPoint;
    private float groundedTimer;
    private float cooldown;
    private int climbHolds;
    private bool collisionOff;

    private CharacterController controller;
    private AudioSource audioSource;
    private Animator fadeAnimator;

    // The authority on whether the player is hanging off something
    private ClimbProvider climbProvider;
    private bool providerClimbing;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        audioSource = GetComponent<AudioSource>();

        if (Fade != null) fadeAnimator = Fade.GetComponent<Animator>();

        climbProvider = GetComponentInChildren<ClimbProvider>(true);
        if (climbProvider == null)
            climbProvider = FindAnyObjectByType<ClimbProvider>(FindObjectsInactive.Include);
    }

    void OnEnable()
    {
        ResetFallTracking();
    }

    void Update()
    {
        SyncClimbing();

        // Settling in after a respawn - track nothing, decide nothing
        if (cooldown > 0f)
        {
            cooldown -= Time.deltaTime;
            ResetFallTracking();
            return;
        }

        // On the ladder: hanging is not falling
        if (isClimbing)
        {
            highestPoint = transform.position.y;
            groundedTimer = groundedGrace;
            return;
        }

        float y = transform.position.y;

        if (controller.isGrounded)
            groundedTimer = groundedGrace;
        else
            groundedTimer -= Time.deltaTime;

        // Standing: the baseline follows them, so walking downstairs is free
        if (groundedTimer > 0f)
        {
            highestPoint = y;
            return;
        }

        highestPoint = Mathf.Max(highestPoint, y);

        if (highestPoint - y >= minimumFallHeight)
        {
            Respawn();
            return;
        }

        // Nothing to land on out here
        if (respawnPoint != null &&
            respawnPoint.transform.position.y - y >= limitBelowRespawn)
        {
            Respawn();
        }
    }

    public void ResetFallTracking()
    {
        highestPoint = transform.position.y;
        groundedTimer = groundedGrace;
    }

    // The ClimbProvider knows exactly when the player is hanging off ANY
    // ClimbInteractable. The grab/release events below were only ever wired
    // to one handles object, so climbing on anything else never switched
    // collision off - which is why the pass-through seemed not to work.
    private void SyncClimbing()
    {
        if (climbProvider == null) return;

        bool climbing = climbProvider.climbAnchorInteractable != null;

        if (climbing == providerClimbing) return;

        providerClimbing = climbing;

        if (climbing) BeginClimb();
        else EndClimb();
    }

    // Still wired to the handles' grab and release. Only used when there is no
    // ClimbProvider to ask - otherwise the two would count the same grab twice.
    // Hands are counted: letting go with one while the other holds is not
    // "stopped climbing".
    public void On()
    {
        if (climbProvider != null) return;

        climbHolds++;
        BeginClimb();
    }

    public void Off()
    {
        if (climbProvider != null) return;

        climbHolds = Mathf.Max(0, climbHolds - 1);
        if (climbHolds > 0) return;

        EndClimb();
    }

    private void BeginClimb()
    {
        isClimbing = true;

        if (passThroughWhileClimbing && controller != null && controller.enabled)
        {
            controller.enabled = false;
            collisionOff = true;
        }
    }

    private void EndClimb()
    {
        isClimbing = false;
        RestoreCollision();
        ResetFallTracking();
    }

    // Back on the moment the last hand lets go, so they can stand in the nest
    private void RestoreCollision()
    {
        if (!collisionOff) return;

        collisionOff = false;
        if (controller != null) controller.enabled = true;
    }

    // GAME switches this off between levels - never leave the player without
    // a collider because a level ended mid-climb
    void OnDisable()
    {
        climbHolds = 0;
        providerClimbing = false;
        isClimbing = false;
        RestoreCollision();
    }

    private void Respawn()
    {
        if (respawnPoint != null)
            Teleport(respawnPoint.transform.position);

        if (audioSource != null)
            audioSource.Play();

        // Explicit layer and time: Play("Fall") alone keeps the current time
        // when the animator is already in that state, so every fall after the
        // first resumed from the end of the clip and showed nothing.
        if (fadeAnimator != null)
            fadeAnimator.Play("Fall", 0, 0f);

        cooldown = respawnCooldown;
        ResetFallTracking();
    }

    // Controller ignores raw writes
    private void Teleport(Vector3 destination)
    {
        controller.enabled = false;
        transform.position = destination;
        controller.enabled = true;
    }
}
