using UnityEngine;
using System.Collections.Generic;

// The crow's nest: being up here allows pointing and shows one random target.
//
// Checks the player's position every frame instead of using OnTriggerEnter.
// Fall switches the CharacterController off while climbing (so the player can
// pass through the rigging), which means the player usually reached the nest
// with no collider at all - the trigger never fired, and the ray never came
// out until they let go and landed.
public class NestRandomActivator : MonoBehaviour
{
    [Header("Objects to randomize")]
    public List<GameObject> objectsToToggle = new List<GameObject>();

    [Header("Player detection")]
    public string playerTag = "Player";

    [Tooltip("Seconds outside the nest before pointing switches off, so leaning over the edge does not flicker it.")]
    public float leaveGrace = 0.5f;

    private Collider[] zones;
    private CharacterController body;
    private Transform head;

    private bool playerInside;
    private bool chosenThisRun;
    private float outsideFor;

    private void Awake()
    {
        // The trigger(s) on this object are the nest area - they no longer
        // need to catch anything, they are just the shape to test against
        zones = GetComponents<Collider>();
    }

    private void OnEnable()
    {
        playerInside = false;
        chosenThisRun = false;
        outsideFor = 0f;
    }

    private void OnDisable()
    {
        // Level ended or quit from the menu while up here - do not leave the
        // ray live for the other levels
        if (playerInside) PointerReader.SetAllPointingEnabled(false);
        playerInside = false;
    }

    private void Update()
    {
        bool inside = PlayerInNest();

        if (inside)
        {
            outsideFor = 0f;

            if (!playerInside)
            {
                playerInside = true;

                // Once per run, so stepping out and back in does not swap the
                // bird the player was already looking for
                if (!chosenThisRun)
                {
                    chosenThisRun = true;
                    ActivateRandomObject();
                }

                // Every hand, so a left-handed player can point too
                PointerReader.SetAllPointingEnabled(true);
            }
        }
        else if (playerInside)
        {
            outsideFor += Time.deltaTime;

            if (outsideFor >= leaveGrace)
            {
                playerInside = false;
                PointerReader.SetAllPointingEnabled(false);
            }
        }
    }

    // Body or head inside any of the nest's colliders. Positions only, so it
    // works with the CharacterController switched off mid-climb
    private bool PlayerInNest()
    {
        if (zones == null || zones.Length == 0) return false;

        if (body == null)
        {
            GameObject player = GameObject.FindWithTag(playerTag);
            if (player != null) body = player.GetComponentInChildren<CharacterController>(true);
        }

        if (head == null && Camera.main != null) head = Camera.main.transform;

        if (body != null && Inside(body.transform.TransformPoint(body.center))) return true;
        if (head != null && Inside(head.position)) return true;

        return false;
    }

    private bool Inside(Vector3 point)
    {
        foreach (Collider zone in zones)
        {
            if (zone == null) continue;

            // A point inside the shape is its own closest point. Works for
            // box, sphere, capsule and convex mesh colliders
            if ((zone.ClosestPoint(point) - point).sqrMagnitude < 0.0001f)
                return true;
        }

        return false;
    }

    // Called by GAME when L1 ends
    public void HideAll()
    {
        chosenThisRun = false;

        if (objectsToToggle == null) return;

        foreach (GameObject obj in objectsToToggle)
        {
            if (obj != null) obj.SetActive(false);
        }
    }

    private void ActivateRandomObject()
    {
        if (objectsToToggle == null || objectsToToggle.Count == 0)
        {
            Debug.LogWarning("No objects assigned in objectsToToggle.", this);
            return;
        }

        foreach (GameObject obj in objectsToToggle)
        {
            if (obj != null)
                obj.SetActive(false);
        }

        GameObject chosen = objectsToToggle[Random.Range(0, objectsToToggle.Count)];

        if (chosen != null)
            chosen.SetActive(true);
    }
}
