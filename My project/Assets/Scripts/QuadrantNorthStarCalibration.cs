using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class QuadrantNorthStarCalibration : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private XRGrabInteractable grabInteractable;
    [SerializeField] private Transform northStar;
    [SerializeField] private Transform aimReference;

    [Header("Ghost Prefab")]
    [SerializeField] private GameObject ghostQuadrantPrefab;

    [Header("Phase 1 - Initial Lock")]
    [SerializeField] private float initialAlignmentAngle = 8f;
    [SerializeField] private float initialLockTime = 2f;

    [Header("Phase 2 - Hold In Place")]
    [SerializeField] private float finalAlignmentAngle = 8f;
    [SerializeField] private float requiredHoldTime = 10f;

    [Tooltip("How far the real quadrant can move away from the ghost position.")]
    [SerializeField] private float allowedPositionOffset = 0.08f;

    [Tooltip("How far the real quadrant can rotate away from the ghost rotation.")]
    [SerializeField] private float allowedRotationOffset = 10f;

    [Tooltip("How long the player can be invalid before reset.")]
    [SerializeField] private float leaveGraceTime = 2f;

    [Tooltip("Also require pointing at the star while holding. Off: matching the ghost is enough - the rocking ghost can drift off the exact sighting line, which made the hold fail unfairly. Phase 1 still needs the star to spawn the ghost.")]
    [SerializeField] private bool requireStarDuringHold = false;

    [Header("Ghost Rocking")]
    [Tooltip("Ride the ship, so the deck's roll is not seen as the ghost swinging about. The fake roll below is then the only motion to chase. Off = the old world-fixed ghost, which means fighting the full roll and pitch.")]
    [SerializeField] private bool ghostRidesShip = true;

    [Tooltip("Roll the ghost a little, as though it were riding the deck, so the player has to follow it.")]
    [SerializeField] private bool rockGhost = true;

    [Tooltip("Degrees either side of the pose it was spawned at. Never exceeded.")]
    [SerializeField] private float rockAngle = 6f;

    [Tooltip("Degrees per second it swings at.")]
    [SerializeField] private float rockSpeed = 4f;

    [Tooltip("Seconds between picking a new target, min and max. It re-aims before arriving, so it can turn back from halfway - that is what stops it reading as a metronome.")]
    [SerializeField] private Vector2 rockChangeEvery = new(0.6f, 2.2f);

    [Header("Events")]
    public UnityEvent onCalibrationCompleted;
    public UnityEvent onGhostSpawned;
    public UnityEvent onReset;
    public UnityEvent onLeaveGraceStarted;
    public UnityEvent onRecoveredFromGrace;
    public UnityEvent onResetForReplay;

    private bool isGrabbed;
    private bool ghostSpawned;
    private bool completed;
    private bool inLeaveGrace;

    private float initialLockTimer;
    private float finalHoldTimer;
    private float invalidTimer;

    private GameObject currentGhost;

    // The completion event freezes and recolours the quadrant; undo that on replay
    private Rigidbody[] bodies;
    private bool[] bodiesKinematic;
    private Renderer[] renderers;
    private Material[][] rendererMaterials;

    // The pointer is frozen mid-swing, so its pose has to come back too
    private Transform[] parts;
    private Vector3[] partPositions;
    private Quaternion[] partRotations;

    // The ghost's live pose, which is what the player has to match
    private Vector3 ghostPosition;
    private Quaternion ghostRotation;

    // Rest pose and swing axis, both local to whatever the ghost hangs off
    private Transform ghostParent;
    private Quaternion ghostRestLocalRotation;
    private Vector3 rockAxis;
    private float rockCurrent;
    private float rockTarget;
    private float rockRetarget;

    private void Reset()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();
        aimReference = transform;
    }

    private void Awake()
    {
        bodies = GetComponentsInChildren<Rigidbody>(true);
        bodiesKinematic = new bool[bodies.Length];
        for (int i = 0; i < bodies.Length; i++) bodiesKinematic[i] = bodies[i].isKinematic;

        renderers = GetComponentsInChildren<Renderer>(true);
        rendererMaterials = new Material[renderers.Length][];
        for (int i = 0; i < renderers.Length; i++)
            rendererMaterials[i] = renderers[i].sharedMaterials;

        parts = GetComponentsInChildren<Transform>(true);
        partPositions = new Vector3[parts.Length];
        partRotations = new Quaternion[parts.Length];

        for (int i = 0; i < parts.Length; i++)
        {
            partPositions[i] = parts[i].localPosition;
            partRotations[i] = parts[i].localRotation;
        }
    }

    private void OnEnable()
    {
        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.AddListener(OnGrabbed);
            grabInteractable.selectExited.AddListener(OnReleased);
        }

        RestoreForReplay();
    }

    // Put the quadrant back to loose and unpainted
    private void RestoreForReplay()
    {
        completed = false;

        // Pose first, so physics wakes up already back in place
        if (parts != null)
        {
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i] == null) continue;

                parts[i].localPosition = partPositions[i];
                parts[i].localRotation = partRotations[i];
            }
        }

        if (bodies != null)
        {
            for (int i = 0; i < bodies.Length; i++)
            {
                if (bodies[i] == null) continue;

                bodies[i].isKinematic = bodiesKinematic[i];

                if (!bodies[i].isKinematic)
                {
                    bodies[i].linearVelocity = Vector3.zero;
                    bodies[i].angularVelocity = Vector3.zero;
                }
            }
        }

        if (renderers != null)
        {
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null) renderers[i].sharedMaterials = rendererMaterials[i];
        }

        onResetForReplay.Invoke();
    }

    private void OnDisable()
    {
        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.RemoveListener(OnGrabbed);
            grabInteractable.selectExited.RemoveListener(OnReleased);
        }

        isGrabbed = false;
        completed = false;
        ClearGhost();
    }

    private void Update()
    {
        if (!isGrabbed || completed || northStar == null || aimReference == null)
            return;

        if (!ghostSpawned)
            HandleInitialAlignment();
        else
            HandleFinalHoldPhase();
    }

    private void HandleInitialAlignment()
    {
        if (IsAlignedWithStar(initialAlignmentAngle))
        {
            initialLockTimer += Time.deltaTime;

            if (initialLockTimer >= initialLockTime)
                SpawnGhostAtCurrentPose();
        }
        else
        {
            initialLockTimer = 0f;
        }
    }

    private void HandleFinalHoldPhase()
    {
        RockGhost();

        bool isValid = IsWithinGhostArea() &&
                       (!requireStarDuringHold || IsAlignedWithStar(finalAlignmentAngle));

        if (isValid)
        {
            if (inLeaveGrace)
            {
                inLeaveGrace = false;
                onRecoveredFromGrace.Invoke();
            }

            finalHoldTimer += Time.deltaTime;
            invalidTimer = 0f;

            if (finalHoldTimer >= requiredHoldTime)
                CompleteCalibration();
        }
        else
        {
            if (!inLeaveGrace)
            {
                inLeaveGrace = true;
                onLeaveGraceStarted.Invoke();
            }

            invalidTimer += Time.deltaTime;

            if (invalidTimer >= leaveGraceTime)
                ResetCalibration();
        }
    }

    // Rolls the ghost about its own aim axis. Any rotation leaves vectors
    // parallel to its axis untouched, so the quadrant's sighting direction is
    // mathematically unchanged - the ghost never stops pointing at the star, it
    // only leans. Rocking about any other axis would swing the aim off and make
    // the two checks impossible to satisfy at once.
    //
    // This is why the ghost is still not parented to the ship: the deck's real
    // roll is about the hull's axis, which would take the aim with it.
    private void RockGhost()
    {
        if (currentGhost == null) return;

        if (rockGhost)
        {
            rockRetarget -= Time.deltaTime;

            // Re-aim on a timer rather than on arrival, so it can change its
            // mind halfway and swing back without ever having got there. A
            // sine always completed the stroke, which read as a metronome.
            if (rockRetarget <= 0f)
                PickRockTarget();

            rockCurrent = Mathf.MoveTowards(rockCurrent, rockTarget, rockSpeed * Time.deltaTime);

            currentGhost.transform.localRotation =
                Quaternion.AngleAxis(rockCurrent, rockAxis) * ghostRestLocalRotation;
        }

        // Whatever the ghost is actually doing is what has to be matched -
        // read it back rather than recomputing, so this holds whether the
        // ghost is riding the ship or standing still in the world.
        ghostPosition = currentGhost.transform.position;
        ghostRotation = currentGhost.transform.rotation;
    }

    // Anywhere in range, not just the extremes - so some swings are lazy and
    // some go the whole way over. Clamped, so rockAngle is still a hard limit.
    private void PickRockTarget()
    {
        rockTarget = Random.Range(-rockAngle, rockAngle);
        rockRetarget = Random.Range(rockChangeEvery.x, rockChangeEvery.y);
    }

    private bool IsAlignedWithStar(float allowedAngle)
    {
        Vector3 directionToStar = (northStar.position - aimReference.position).normalized;
        return Vector3.Angle(aimReference.forward, directionToStar) <= allowedAngle;
    }

    private bool IsWithinGhostArea()
    {
        return Vector3.Distance(transform.position, ghostPosition) <= allowedPositionOffset &&
               Quaternion.Angle(transform.rotation, ghostRotation) <= allowedRotationOffset;
    }

    private void SpawnGhostAtCurrentPose()
    {
        ghostSpawned = true;

        ghostPosition = transform.position;
        ghostRotation = transform.rotation;

        if (ghostQuadrantPrefab != null)
        {
            currentGhost = Instantiate(ghostQuadrantPrefab, ghostPosition, ghostRotation);

            // Riding the ship means the deck's roll moves the player and the
            // ghost together, so it reads as still. The north star is a child
            // of the ship too, so the sighting stays true in the same frame.
            ghostParent = ghostRidesShip ? ShipRoot.Transform : null;

            if (ghostParent != null)
                currentGhost.transform.SetParent(ghostParent, true);

            ghostRestLocalRotation = currentGhost.transform.localRotation;

            Vector3 aim = aimReference != null ? aimReference.forward : transform.forward;
            rockAxis = ghostParent != null
                ? ghostParent.InverseTransformDirection(aim)
                : aim;

            rockCurrent = 0f;
            PickRockTarget();
        }

        initialLockTimer = 0f;
        finalHoldTimer = 0f;
        invalidTimer = 0f;
        inLeaveGrace = false;

        onGhostSpawned.Invoke();
    }

    private void CompleteCalibration()
    {
        completed = true;

        ClearGhost();
        onCalibrationCompleted.Invoke();
    }

    private void ResetCalibration()
    {
        if (completed) return;

        ClearGhost();
        onReset.Invoke();
    }

    private void ClearGhost()
    {
        ghostSpawned = false;
        inLeaveGrace = false;

        initialLockTimer = 0f;
        finalHoldTimer = 0f;
        invalidTimer = 0f;

        if (currentGhost != null)
        {
            Destroy(currentGhost);
            currentGhost = null;
        }

        ghostParent = null;
    }

    private void OnGrabbed(SelectEnterEventArgs args)
    {
        isGrabbed = true;
    }

    private void OnReleased(SelectExitEventArgs args)
    {
        isGrabbed = false;
        ResetCalibration();
    }

    public float GetInitialProgress01()
    {
        if (initialLockTime <= 0f) return 1f;
        return Mathf.Clamp01(initialLockTimer / initialLockTime);
    }

    public float GetFinalProgress01()
    {
        if (requiredHoldTime <= 0f) return 1f;
        return Mathf.Clamp01(finalHoldTimer / requiredHoldTime);
    }

    public bool HasGhostSpawned() => ghostSpawned;

    public bool IsCurrentlyInLeaveGrace() => ghostSpawned && inLeaveGrace;

    public bool IsCurrentlyAlignedWithStar()
    {
        if (northStar == null || aimReference == null) return false;
        return IsAlignedWithStar(finalAlignmentAngle);
    }

    public bool IsCurrentlyWithinGhostArea() => ghostSpawned && IsWithinGhostArea();
}
