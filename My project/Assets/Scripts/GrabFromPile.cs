using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[RequireComponent(typeof(XRSimpleInteractable))]
public class GrabFromPile : MonoBehaviour
{
    [Header("What this pile gives")]
    public XRGrabInteractable itemPrefab;

    [Header("Optional spawn settings")]
    public Transform spawnPoint;

    private XRSimpleInteractable sourceInteractable;
    private XRInteractionManager interactionManager;

    private void Awake()
    {
        sourceInteractable = GetComponent<XRSimpleInteractable>();
    }

    private void OnEnable()
    {
        sourceInteractable.selectEntered.AddListener(OnPileGrabbed);
    }

    private void OnDisable()
    {
        sourceInteractable.selectEntered.RemoveListener(OnPileGrabbed);
    }

    private void OnPileGrabbed(SelectEnterEventArgs args)
    {
        if (itemPrefab == null)
            return;

        IXRSelectInteractor interactor = args.interactorObject;
        if (interactor == null)
            return;

        interactionManager = sourceInteractable.interactionManager;

        if (interactionManager == null)
            interactionManager = FindFirstObjectByType<XRInteractionManager>();

        if (interactionManager == null)
        {
            Debug.LogError("No XRInteractionManager found.", this);
            return;
        }

        Transform origin = spawnPoint != null ? spawnPoint : transform;
        XRGrabInteractable spawned = Instantiate(itemPrefab, origin.position, origin.rotation);
        SpawnedItem.Mark(spawned.gameObject);
        ShipRoot.Attach(spawned.gameObject);

        // Swap pile for item. Unconditionally, because this is a scripted
        // handoff - a NearGrabOnly filter on the item would refuse it,
        // since the item spawns at the pile pivot, not under the hand.
        interactionManager.SelectExit(interactor, sourceInteractable);
        interactionManager.SelectEnterUnconditionally(interactor, spawned);
    }
}
