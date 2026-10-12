using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class CannonLoader : MonoBehaviour
{
    public enum CannonPhase
    {
        Clean,
        Powder,
        Ball,
        Ready
    }

    public CannonPhase currentPhase = CannonPhase.Clean;

    [Header("Animations")]
    public Animator cannonAnimator;

    [Header("Settings")]
    public int powderCount = 0;
    [Tooltip("Loading this many charges blows the cannon up.")]
    public int explodeAtPowder = 4;
    public float powerPerCharge = 20f;
    public bool overloaded = false;
    public bool ballLoaded = false;

    [Tooltip("Things parented here are ignored by the loader. Defaults to the cannon.")]
    public Transform propRoot;

    [Header("Respawn")]
    public GameObject cleaningStickPrefab;
    public Transform cleaningStickSpawnPoint;
    public float stickRespawnDelay = 4f;

    public void TryLoad(GameObject item)
    {
        // The Fake* props are tagged like real ammo and would be eaten
        if (IsOwnProp(item)) return;

        GameObject root = ResolveRoot(item);

        if (currentPhase == CannonPhase.Clean && root.CompareTag("CleaningStick"))
        {
            Consume(root);
            cannonAnimator.SetTrigger("Clean");
            currentPhase = CannonPhase.Powder;

            StartCoroutine(RespawnCleaningStick());
            return;
        }

        if ((currentPhase == CannonPhase.Powder || currentPhase == CannonPhase.Ball)
            && root.CompareTag("Powder"))
        {
            Consume(root);
            powderCount++;
            cannonAnimator.SetTrigger("Powder");

            currentPhase = CannonPhase.Ball;
            overloaded = powderCount >= explodeAtPowder;
            return;
        }

        if (currentPhase == CannonPhase.Ball && root.CompareTag("CannonBall"))
        {
            Consume(root);
            ballLoaded = true;
            cannonAnimator.SetTrigger("Ball");
            currentPhase = CannonPhase.Ready;
        }
    }

    public void ResetCannon()
    {
        currentPhase = CannonPhase.Clean;
        powderCount = 0;
        overloaded = false;
        ballLoaded = false;
    }

    // The scene stick is consumed on first use, so hand out a fresh one
    public void ResetForLevel()
    {
        StopAllCoroutines();
        ResetCannon();

        if (GameObject.FindGameObjectWithTag("CleaningStick") == null)
            SpawnCleaningStick();
    }

    public float GetPower()
    {
        return powderCount * powerPerCharge;
    }

    // Anything under the cannon is scenery, not ammunition
    private bool IsOwnProp(GameObject item)
    {
        Transform root = propRoot != null ? propRoot : transform;
        return item != null && item.transform.IsChildOf(root);
    }

    // Tag is on root
    private GameObject ResolveRoot(GameObject item)
    {
        XRGrabInteractable grab = item.GetComponentInParent<XRGrabInteractable>();
        if (grab != null) return grab.gameObject;

        Rigidbody body = item.GetComponentInParent<Rigidbody>();
        return body != null ? body.gameObject : item;
    }

    // Drop before destroying
    private void Consume(GameObject item)
    {
        XRGrabInteractable grab = item.GetComponent<XRGrabInteractable>();

        if (grab != null && grab.isSelected && grab.interactionManager != null)
            grab.interactionManager.CancelInteractableSelection((IXRSelectInteractable)grab);

        Destroy(item);
    }

    private IEnumerator RespawnCleaningStick()
    {
        yield return new WaitForSeconds(stickRespawnDelay);
        SpawnCleaningStick();
    }

    private void SpawnCleaningStick()
    {
        if (cleaningStickPrefab == null || cleaningStickSpawnPoint == null) return;

        GameObject stick = Instantiate(
            cleaningStickPrefab,
            cleaningStickSpawnPoint.position,
            cleaningStickSpawnPoint.rotation
        );

        SpawnedItem.Mark(stick);
        ShipRoot.Attach(stick);
    }
}
