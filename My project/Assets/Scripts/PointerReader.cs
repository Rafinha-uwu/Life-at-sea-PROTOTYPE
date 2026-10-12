using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals;

// The spotting ray up the mast. Two conditions, deliberately separate:
//   pointingEnabled - the level says pointing is allowed at all (top of mast)
//   the point input  - the player is actually holding their finger out
// Both must be true, so the ray is not just permanently on once you arrive.
public class PointerReader : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private XRRayInteractor rayInteractor;
    [SerializeField] private GameObject lineVisual;

    [Header("State")]
    [SerializeField] private bool pointingEnabled = false;

    [Header("Hold To Point")]
    [Tooltip("Ray only appears while the point input is held. Off restores the old always-on behaviour.")]
    [SerializeField] private bool requireHold = true;

    [Tooltip("Leave empty to use the trigger - the same input that makes the hand point.")]
    [SerializeField] private InputActionProperty pointInput;

    public enum Hand { Auto, Left, Right, Either }

    [Tooltip("Which hand's trigger this pointer listens to. Auto works it out from the controller it sits under.")]
    [SerializeField] private Hand hand = Hand.Auto;

    // Every pointer, one per hand, so puzzles can switch them all together
    private static readonly System.Collections.Generic.List<PointerReader> All = new();

    private PointableTarget currentTarget;
    private XRInteractorLineVisual cachedLineVisual;
    private InputAction fallbackAction;
    private bool lastActive;

    private void Reset()
    {
        rayInteractor = GetComponent<XRRayInteractor>();
    }

    private void Awake()
    {
        if (lineVisual != null)
            cachedLineVisual = lineVisual.GetComponent<XRInteractorLineVisual>();

        All.Add(this);

        // Same fallback trick the pause menu uses, so this needs no wiring.
        // Bound to THIS pointer's own hand: with one under each controller,
        // binding both triggers made either trigger light up both rays.
        Hand side = ResolveHand();

        fallbackAction = new InputAction("Point", InputActionType.Button);

        if (side != Hand.Left)
        {
            fallbackAction.AddBinding("<XRController>{RightHand}/trigger");
            fallbackAction.AddBinding("<XRController>{RightHand}/triggerPressed");
        }

        if (side != Hand.Right)
        {
            fallbackAction.AddBinding("<XRController>{LeftHand}/trigger");
            fallbackAction.AddBinding("<XRController>{LeftHand}/triggerPressed");
        }

        fallbackAction.AddBinding("<Mouse>/leftButton");

        ApplyLineVisual(false);
    }

    // Auto reads "Left"/"Right" off the controller this sits under, so a copy
    // dropped under the other controller needs no setting changed
    private Hand ResolveHand()
    {
        if (hand != Hand.Auto) return hand;

        for (Transform t = transform; t != null; t = t.parent)
        {
            string n = t.name.ToLowerInvariant();
            if (n.Contains("left")) return Hand.Left;
            if (n.Contains("right")) return Hand.Right;
        }

        return Hand.Either;
    }

    // Puzzles turn pointing on and off for every hand at once - the player
    // uses whichever they prefer
    public static void SetAllPointingEnabled(bool value)
    {
        foreach (PointerReader reader in All)
        {
            if (reader != null) reader.SetPointingEnabled(value);
        }
    }

    // Any hand actually pointing right now
    public static bool AnyPointing
    {
        get
        {
            foreach (PointerReader reader in All)
            {
                if (reader != null && reader.IsPointing) return true;
            }

            return false;
        }
    }

    // Statics survive "enter play mode without domain reload", so wipe them
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        All.Clear();
    }

    private void OnEnable()
    {
        if (ActiveAction == fallbackAction)
            fallbackAction.Enable();
    }

    private void OnDisable()
    {
        if (ActiveAction == fallbackAction)
            fallbackAction.Disable();

        ClearCurrentTarget();
        ApplyLineVisual(false);
        lastActive = false;
    }

    private void OnDestroy()
    {
        All.Remove(this);
        fallbackAction?.Dispose();
    }

    // An unassigned property returns an empty action, not null
    private InputAction ActiveAction
    {
        get
        {
            InputAction assigned = pointInput.action;

            if (assigned != null && assigned.bindings.Count > 0)
                return assigned;

            return fallbackAction;
        }
    }

    private bool Holding
    {
        get
        {
            if (!requireHold) return true;

            InputAction action = ActiveAction;
            return action != null && action.IsPressed();
        }
    }

    private void Update()
    {
        bool active = pointingEnabled && Holding;

        if (active != lastActive)
        {
            lastActive = active;
            ApplyLineVisual(active);

            // Let go mid-hover and the target should stop counting up
            if (!active) ClearCurrentTarget();
        }

        if (!active || rayInteractor == null)
            return;

        if (!rayInteractor.TryGetCurrent3DRaycastHit(out RaycastHit hit))
        {
            ClearCurrentTarget();
            return;
        }

        // Colliders sit on children
        PointableTarget newTarget = hit.collider.GetComponentInParent<PointableTarget>();

        if (newTarget != currentTarget)
        {
            ClearCurrentTarget();
            currentTarget = newTarget;

            if (currentTarget != null)
                currentTarget.OnPointEnter(hit);
        }

        if (currentTarget != null)
            currentTarget.OnPointStay(hit);
    }

    public void SetPointingEnabled(bool value)
    {
        pointingEnabled = value;

        if (!value)
        {
            ClearCurrentTarget();
            ApplyLineVisual(false);
            lastActive = false;
        }
    }

    public bool IsPointingEnabled => pointingEnabled;

    // Allowed AND actually held - the ray is out right now
    public bool IsPointing => lastActive;

    private void ApplyLineVisual(bool visible)
    {
        if (cachedLineVisual != null)
            cachedLineVisual.enabled = visible;
    }

    private void ClearCurrentTarget()
    {
        if (currentTarget != null)
        {
            currentTarget.OnPointExit();
            currentTarget = null;
        }
    }
}
