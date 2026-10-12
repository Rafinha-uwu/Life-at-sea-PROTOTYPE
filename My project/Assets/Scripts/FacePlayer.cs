using UnityEngine;

// Turns a world-space panel to face the player, the way the sailors' speech
// bubbles do. Rotation only by default: the info panels belong on their door or
// barrel, they just should not be read edge-on.
//
// Runs in LateUpdate, after the Animator and after the headset pose has landed,
// so the panel never trails a frame behind the player's head. It only touches
// rotation, and the InfoIn/InfoOut clips only animate scale and sprite, so the
// two do not fight.
//
// Side effect worth knowing: on a panel parented to the rocking ship, facing
// the player in world space also cancels the roll, so the text stays level
// instead of tilting with the deck. That is usually what you want for text.
public class FacePlayer : MonoBehaviour
{
    [Tooltip("Leave empty to use the main camera.")]
    public Transform playerCamera;

    [Tooltip("What actually turns. Leave empty to turn this object.")]
    public Transform pivot;

    [Tooltip("Whose 'up' the panel keeps. Empty = the ship root, when the panel rides it - so panels heel with the deck instead of standing bolt upright against a rolling ship.")]
    public Transform upReference;

    [Header("Facing")]
    [Tooltip("Turn only around the up axis, so the panel stays level relative to the deck. Off lets it tilt to meet the player's eyeline - better for panels well above or below head height.")]
    public bool keepUpright = true;

    [Tooltip("Degrees per second. 0 snaps, which is what the speech bubbles do.")]
    public float turnSpeed = 0f;

    [Header("Come into view (optional)")]
    [Tooltip("The other half of what the speech bubbles do: if the player looks away, bring the panel round in front of them. Off by default - a panel that closes when you stop pointing at it never needs this.")]
    public bool bringIntoView = false;

    public float lookAwayAngle = 50f;
    public float viewDistance = 1.5f;
    public float moveSpeed = 4f;

    private Vector3 homeLocalPosition;
    private bool homeCaptured;

    private Transform Target => pivot != null ? pivot : transform;

    private void OnEnable()
    {
        if (playerCamera == null && Camera.main != null)
            playerCamera = Camera.main.transform;

        // Resolved once - IsChildOf walks the hierarchy, and this never changes
        if (upReference == null)
        {
            Transform ship = ShipRoot.Transform;

            if (ship != null && transform.IsChildOf(ship))
                upReference = ship;
        }

        // Local: the panel rides the ship, so a world home would drift
        if (!homeCaptured)
        {
            homeLocalPosition = Target.localPosition;
            homeCaptured = true;
        }
    }

    private void LateUpdate()
    {
        if (playerCamera == null) return;

        Face();

        if (bringIntoView) Reposition();
    }

    private void Face()
    {
        Transform panel = Target;

        // Same direction the speech bubbles use: a canvas shows its +Z face,
        // so it points away from the viewer to be read.
        Vector3 away = panel.position - playerCamera.position;

        // The ship's up, not the world's. Turning about the deck's axis keeps
        // the panel heeling with the ship - level against the world would read
        // as though it had come loose from the thing it labels.
        Vector3 up = upReference != null ? upReference.up : Vector3.up;

        if (keepUpright) away = Vector3.ProjectOnPlane(away, up);
        if (away.sqrMagnitude < 0.0001f) return;

        Quaternion wanted = Quaternion.LookRotation(away.normalized, up);

        panel.rotation = turnSpeed > 0f
            ? Quaternion.RotateTowards(panel.rotation, wanted, turnSpeed * Time.unscaledDeltaTime)
            : wanted;
    }

    private void Reposition()
    {
        Transform panel = Target;

        float angle = Vector3.Angle(playerCamera.forward, panel.position - playerCamera.position);

        Vector3 wanted = angle > lookAwayAngle
            ? playerCamera.position + playerCamera.forward * viewDistance
            : Home(panel);

        panel.position = moveSpeed > 0f
            ? Vector3.Lerp(panel.position, wanted, moveSpeed * Time.unscaledDeltaTime)
            : wanted;
    }

    private Vector3 Home(Transform panel)
    {
        return panel.parent != null
            ? panel.parent.TransformPoint(homeLocalPosition)
            : homeLocalPosition;
    }
}
