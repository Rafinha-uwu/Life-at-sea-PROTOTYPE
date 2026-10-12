using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// A capstan the player can push round: walk into the bars, or grab one and
// walk it round like the crew would. It turns on one axis only, carries on
// under its own weight and slows with drag.
//
// Not physics on purpose. The player moves on a CharacterController, which
// does not push Rigidbodies - against a solid bar it simply stops, and a
// stopped player has no speed to give. So this reads how fast each pusher is
// moving AROUND the axis and turns that into spin.
//
// Everything is measured in the ship's own space, so the deck rocking the
// player and the capstan together is not mistaken for a push.
public class Capstan : MonoBehaviour
{
    [Header("Spin")]
    [Tooltip("The part that turns. Leave empty to turn this object.")]
    public Transform spinner;

    [Tooltip("The axis it turns on, in the spinner's own space. Z by default.")]
    public Vector3 localAxis = Vector3.forward;

    [Tooltip("How fast it slows down on its own. Higher stops sooner.")]
    public float drag = 0.8f;

    [Tooltip("How strongly a push drags it along. Low feels heavy and sluggish, high follows you exactly.")]
    public float grip = 3f;

    [Tooltip("Top speed, degrees per second.")]
    public float maxSpeed = 120f;

    [Tooltip("Slower movement than this is ignored, so standing in it does not jitter it.")]
    public float minimumPushSpeed = 0.15f;

    [Header("Push with the body")]
    [Tooltip("Needs a TRIGGER collider on this object around the bars. Walking through it pushes.")]
    public bool bodyPush = true;

    [Header("Turn with the hands")]
    [Tooltip("Optional. An XRSimpleInteractable on the bars: grab and walk it round. Must not be a grab interactable, or grabbing would pull the capstan off.")]
    public XRBaseInteractable handle;

    private Quaternion restLocalRotation;
    private float angle;
    private float speed;

    private CharacterController body;
    private Vector3 lastBodyPosition;

    private Transform hand;
    private Vector3 lastHandPosition;

    private Transform Spinner => spinner != null ? spinner : transform;

    private void Awake()
    {
        restLocalRotation = Spinner.localRotation;
    }

    private void OnEnable()
    {
        if (handle == null) return;

        handle.selectEntered.AddListener(OnGrabbed);
        handle.selectExited.AddListener(OnReleased);
    }

    private void OnDisable()
    {
        if (handle != null)
        {
            handle.selectEntered.RemoveListener(OnGrabbed);
            handle.selectExited.RemoveListener(OnReleased);
        }

        body = null;
        hand = null;
    }

    private void OnGrabbed(SelectEnterEventArgs args)
    {
        hand = args.interactorObject.transform;
        lastHandPosition = ToShip(hand.position);
    }

    private void OnReleased(SelectExitEventArgs args)
    {
        // Let go: it keeps the speed it had, and drag takes it from there
        hand = null;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!bodyPush) return;

        CharacterController controller = other as CharacterController ?? other.GetComponentInParent<CharacterController>();
        if (controller == null) return;

        body = controller;
        lastBodyPosition = ToShip(BodyCentre());
    }

    private void OnTriggerExit(Collider other)
    {
        if (body != null && (other == body || other.GetComponentInParent<CharacterController>() == body))
            body = null;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        float pushed = 0f;
        bool anyPush = false;

        if (body != null)
        {
            Vector3 position = ToShip(BodyCentre());
            if (Push(position, lastBodyPosition, dt, out float bodySpeed)) { pushed = bodySpeed; anyPush = true; }
            lastBodyPosition = position;
        }

        if (hand != null)
        {
            Vector3 position = ToShip(hand.position);

            // Body and hand together - whichever is pushing harder wins,
            // rather than adding up to twice the speed
            if (Push(position, lastHandPosition, dt, out float handSpeed) &&
                (!anyPush || Mathf.Abs(handSpeed) > Mathf.Abs(pushed)))
            {
                pushed = handSpeed;
                anyPush = true;
            }

            lastHandPosition = position;
        }

        // Pushing pulls the spin towards the push - from either side, so
        // pushing against it slows it and then turns it round
        if (anyPush)
            speed = Mathf.Lerp(speed, pushed, 1f - Mathf.Exp(-grip * dt));

        speed *= Mathf.Exp(-drag * dt);
        speed = Mathf.Clamp(speed, -maxSpeed, maxSpeed);

        angle = Mathf.Repeat(angle + speed * dt, 360f);
        Spinner.localRotation = restLocalRotation * Quaternion.AngleAxis(angle, localAxis);
    }

    // Degrees per second this pusher is asking for: its speed along the
    // circle it is on, divided by how far out it is
    private bool Push(Vector3 position, Vector3 last, float dt, out float degreesPerSecond)
    {
        degreesPerSecond = 0f;

        Vector3 axis = (restLocalRotation * localAxis).normalized;
        Vector3 outward = Vector3.ProjectOnPlane(position - Spinner.localPosition, axis);

        float radius = outward.magnitude;
        if (radius < 0.05f) return false;

        Vector3 velocity = (position - last) / dt;
        Vector3 tangent = Vector3.Cross(axis, outward).normalized;

        float along = Vector3.Dot(velocity, tangent);
        if (Mathf.Abs(along) < minimumPushSpeed) return false;

        degreesPerSecond = along / radius * Mathf.Rad2Deg;
        return true;
    }

    // XRI keeps the controller's centre under the head, so this follows the
    // player even when they walk in the room rather than with the stick
    private Vector3 BodyCentre()
    {
        return body.transform.TransformPoint(body.center);
    }

    private Vector3 ToShip(Vector3 world)
    {
        Transform ship = Spinner.parent;
        return ship != null ? ship.InverseTransformPoint(world) : world;
    }
}
