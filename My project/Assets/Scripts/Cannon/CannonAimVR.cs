using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class CannonAimVR : XRBaseInteractable
{
    [Header("Cannon Pivots")]
    public Transform horizontalPivot;
    public Transform verticalPivot;

    [Tooltip("Rotation centre - the trunnion/base. Defaults to the horizontal pivot.")]
    public Transform aimOrigin;

    [Tooltip("Keep aimOrigin pinned while the barrel swings around it.")]
    public bool pivotAroundAimOrigin = true;

    [Header("Limits (degrees from rest)")]
    public float minVerticalAngle = -10f;
    public float maxVerticalAngle = 45f;
    public float maxHorizontalAngle = 35f;

    [Header("Feel")]
    [Tooltip("1 = barrel follows the hand exactly. Lower is heavier.")]
    [Range(0.1f, 2f)] public float sensitivity = 1f;

    [Tooltip("Higher is slower and heavier.")]
    [Range(0f, 1f)] public float smoothTime = 0.18f;

    [Tooltip("Ignore the hand this close to the pivot - the angle goes wild there.")]
    public float minLeverArm = 0.2f;

    [Tooltip("Hand up = barrel up. Untick if it feels backwards.")]
    public bool invertVertical = true;

    [Tooltip("Untick if left/right feels backwards.")]
    public bool invertHorizontal = false;

    private Transform hand;
    private Transform aimSpace;

    private Quaternion restYaw;
    private Quaternion restPitch;
    private Vector3 pivotRestLocalPosition;
    private Vector3 aimOriginRestLocal;

    private float currentYaw, currentPitch;
    private float yawVelocity, pitchVelocity;

    // Accumulated from frame-to-frame deltas, never from raw angles
    private float aimYaw, aimPitch;
    private float previousRawYaw, previousRawPitch;
    private bool hasPrevious;

    protected override void Awake()
    {
        base.Awake();

        if (horizontalPivot != null)
        {
            restYaw = horizontalPivot.localRotation;
            aimSpace = horizontalPivot.parent;
            pivotRestLocalPosition = horizontalPivot.localPosition;
        }

        if (verticalPivot != null)
            restPitch = verticalPivot.localRotation;

        if (aimOrigin == null)
            aimOrigin = horizontalPivot;

        if (aimSpace != null && aimOrigin != null)
            aimOriginRestLocal = aimSpace.InverseTransformPoint(aimOrigin.position);
    }

    protected override void OnSelectEntered(SelectEnterEventArgs args)
    {
        base.OnSelectEntered(args);

        hand = args.interactorObject.transform;
        hasPrevious = false;
    }

    protected override void OnSelectExited(SelectExitEventArgs args)
    {
        base.OnSelectExited(args);

        hand = null;
        hasPrevious = false;
    }

    private void Update()
    {
        if (horizontalPivot == null || verticalPivot == null)
            return;

        if (hand != null)
            TrackHand();

        currentYaw = Mathf.SmoothDamp(currentYaw, aimYaw, ref yawVelocity, smoothTime);
        currentPitch = Mathf.SmoothDamp(currentPitch, aimPitch, ref pitchVelocity, smoothTime);

        horizontalPivot.localRotation = restYaw * Quaternion.Euler(0f, currentYaw, 0f);
        verticalPivot.localRotation = restPitch * Quaternion.Euler(-currentPitch, 0f, 0f);

        PinAimOrigin();
    }

    private void TrackHand()
    {
        if (!ReadHandAngles(out float rawYaw, out float rawPitch))
        {
            // Too close to the pivot - resync next valid frame instead of jumping
            hasPrevious = false;
            return;
        }

        if (!hasPrevious)
        {
            previousRawYaw = rawYaw;
            previousRawPitch = rawPitch;
            hasPrevious = true;
            return;
        }

        // DeltaAngle survives the +180/-180 wrap that made the barrel snap
        float deltaYaw = Mathf.DeltaAngle(previousRawYaw, rawYaw);
        float deltaPitch = Mathf.DeltaAngle(previousRawPitch, rawPitch);

        if (invertHorizontal) deltaYaw = -deltaYaw;
        if (invertVertical) deltaPitch = -deltaPitch;

        previousRawYaw = rawYaw;
        previousRawPitch = rawPitch;

        // Clamp the accumulator itself, so reversing at a limit responds instantly
        aimYaw = Mathf.Clamp(
            aimYaw + deltaYaw * sensitivity, -maxHorizontalAngle, maxHorizontalAngle);

        aimPitch = Mathf.Clamp(
            aimPitch + deltaPitch * sensitivity, minVerticalAngle, maxVerticalAngle);
    }

    // Transforms spin about their own origin, so slide the whole
    // assembly back until aimOrigin sits where it started
    private void PinAimOrigin()
    {
        if (!pivotAroundAimOrigin || aimSpace == null || aimOrigin == null)
            return;

        if (aimOrigin == horizontalPivot)
            return;

        horizontalPivot.localPosition = pivotRestLocalPosition;

        Vector3 drift = aimOriginRestLocal - aimSpace.InverseTransformPoint(aimOrigin.position);
        horizontalPivot.localPosition += drift;
    }

    // Hand angle measured about the trunnion, not the parent origin
    private bool ReadHandAngles(out float yaw, out float pitch)
    {
        yaw = 0f;
        pitch = 0f;

        if (hand == null || aimSpace == null || aimOrigin == null)
            return false;

        Vector3 rel = aimSpace.InverseTransformPoint(hand.position) - aimOriginRestLocal;

        if (rel.magnitude < minLeverArm)
            return false;

        float flat = new Vector2(rel.x, rel.z).magnitude;
        if (flat < 0.0001f) return false;

        yaw = Mathf.Atan2(rel.x, rel.z) * Mathf.Rad2Deg;
        pitch = Mathf.Atan2(rel.y, flat) * Mathf.Rad2Deg;
        return true;
    }
}
