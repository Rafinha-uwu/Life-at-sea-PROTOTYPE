using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

// Drop on a grabbable to refuse ray/far grabs.
// Uses XRI's select filter, so the XRGrabInteractable keeps its settings.
[RequireComponent(typeof(XRBaseInteractable))]
public class NearGrabOnly : MonoBehaviour, IXRSelectFilter
{
    [Tooltip("Hand must be at least this close to pick the item up.")]
    public float maxGrabDistance = 0.25f;

    [Tooltip("Also refuse a Near-Far interactor that is in its far region.")]
    public bool rejectFarRegion = true;

    [Tooltip("Logs why a grab was allowed or refused.")]
    public bool debugGrabs = false;

    private XRBaseInteractable interactable;

    // Our own list. XRBaseInteractable.colliders strips triggers,
    // and every pile in this project uses a trigger collider.
    private Collider[] ownColliders;

    public bool canProcess => isActiveAndEnabled;

    private void Awake()
    {
        interactable = GetComponent<XRBaseInteractable>();
        ownColliders = GetComponentsInChildren<Collider>(true);
    }

    private void OnEnable()
    {
        interactable.selectFilters.Add(this);
    }

    private void OnDisable()
    {
        interactable.selectFilters.Remove(this);
    }

    public bool Process(IXRSelectInteractor interactor, IXRSelectInteractable target)
    {
        // Only rule on picking up, never on holding. XRI runs select filters
        // every frame for items already in the hand too, and drops anything a
        // filter refuses. On a thumbstick teleport the hand lands at the
        // destination a frame before the item catches up, so this saw a hand
        // metres away, said "too far" - and the item fell where the player had
        // been standing.
        if (interactor.IsSelecting(target))
            return true;

        if (interactor is NearFarInteractor nearFar && rejectFarRegion)
        {
            if (nearFar.selectionRegion.Value == NearFarInteractor.Region.Far)
            {
                Log(interactor, -1f, false, "far region");
                return false;
            }
        }

        Vector3 hand = interactor.transform.position;
        float distance = DistanceToHand(hand);

        bool allowed = distance <= maxGrabDistance;
        Log(interactor, distance, allowed, "distance");

        return allowed;
    }

    private float DistanceToHand(Vector3 hand)
    {
        float closest = float.MaxValue;

        if (ownColliders != null)
        {
            foreach (Collider col in ownColliders)
            {
                if (col == null || !col.enabled) continue;

                float d = Vector3.Distance(col.ClosestPoint(hand), hand);
                if (d < closest) closest = d;
            }
        }

        // No usable collider, so fall back to the object itself
        if (closest == float.MaxValue)
            closest = Vector3.Distance(transform.position, hand);

        return closest;
    }

    private void Log(IXRSelectInteractor interactor, float distance, bool allowed, string reason)
    {
        if (!debugGrabs) return;

        Debug.Log($"{name}: {(allowed ? "ALLOW" : "REFUSE")} by {interactor.transform.name} " +
                  $"({reason}, distance {distance:F3}, limit {maxGrabDistance}, " +
                  $"colliders {(ownColliders != null ? ownColliders.Length : 0)})", this);
    }
}
