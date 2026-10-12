using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class BreakableHandle : MonoBehaviour
{
    [Range(0f, 1f)]
    public float breakChance = 0.6f;

    public float breakDelay = 1f;
    public GameObject brokenHandlePrefab;
    public float debrisLifetime = 8f;

    private Collider[] handleColliders;
    private bool alreadyBreaking;

    private void Awake()
    {
        handleColliders = GetComponentsInChildren<Collider>(true);
    }

    public void TryBreak(
        XRInteractionManager interactionManager,
        IXRSelectInteractor handInteractor,
        IXRSelectInteractable climbInteractable
    )
    {
        if (alreadyBreaking || Random.value > breakChance)
            return;

        alreadyBreaking = true;
        StartCoroutine(BreakAfterDelay(interactionManager, handInteractor, climbInteractable));
    }

    // Called by GAME when L1 restarts
    public void Restore()
    {
        StopAllCoroutines();
        alreadyBreaking = false;

        gameObject.SetActive(true);

        handleColliders ??= GetComponentsInChildren<Collider>(true);

        foreach (Collider col in handleColliders)
        {
            if (col != null) col.enabled = true;
        }
    }

    private IEnumerator BreakAfterDelay(
        XRInteractionManager interactionManager,
        IXRSelectInteractor handInteractor,
        IXRSelectInteractable climbInteractable
    )
    {
        yield return new WaitForSeconds(breakDelay);

        foreach (Collider col in handleColliders)
        {
            if (col != null) col.enabled = false;
        }

        if (interactionManager != null && handInteractor != null && climbInteractable != null &&
            climbInteractable.isSelected)
        {
            interactionManager.SelectExit(handInteractor, climbInteractable);
        }

        if (brokenHandlePrefab != null)
        {
            GameObject debris = Instantiate(
                brokenHandlePrefab, transform.position, transform.rotation);

            ShipRoot.Attach(debris);

            if (debrisLifetime > 0f)
                Destroy(debris, debrisLifetime);
        }

        // Hidden, not destroyed, so L1 can replay
        gameObject.SetActive(false);
    }
}
