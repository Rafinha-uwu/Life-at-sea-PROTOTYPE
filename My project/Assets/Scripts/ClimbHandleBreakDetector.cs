using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[RequireComponent(typeof(XRBaseInteractable))]
public class ClimbHandleBreakDetector : MonoBehaviour
{
    [Tooltip("Ignore handles further than this from the hand.")]
    public float maxGrabDistance = 0.25f;

    private XRBaseInteractable interactable;
    private XRInteractionManager interactionManager;

    private void Awake()
    {
        interactable = GetComponent<XRBaseInteractable>();
        interactionManager = FindFirstObjectByType<XRInteractionManager>();
    }

    private void OnEnable()
    {
        interactable.selectEntered.AddListener(OnGrabbed);
    }

    private void OnDisable()
    {
        interactable.selectEntered.RemoveListener(OnGrabbed);
    }

    private void OnGrabbed(SelectEnterEventArgs args)
    {
        Vector3 handPosition = args.interactorObject.transform.position;

        BreakableHandle grabbed = FindClosestHandle(handPosition);

        if (grabbed != null)
            grabbed.TryBreak(interactionManager, args.interactorObject, interactable);
    }

    // Nearest grabbed rung
    private BreakableHandle FindClosestHandle(Vector3 handPosition)
    {
        BreakableHandle closest = null;
        float closestDistance = maxGrabDistance;

        foreach (Collider col in interactable.colliders)
        {
            if (col == null || !col.enabled)
                continue;

            BreakableHandle handle = col.GetComponentInParent<BreakableHandle>();
            if (handle == null)
                continue;

            float distance = Vector3.Distance(
                col.ClosestPoint(handPosition), handPosition);

            if (distance < closestDistance)
            {
                closestDistance = distance;
                closest = handle;
            }
        }

        return closest;
    }
}
