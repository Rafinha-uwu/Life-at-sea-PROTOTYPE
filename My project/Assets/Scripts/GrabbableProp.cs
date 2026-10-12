using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// Drop on any scene object to make it something the player can pick up.
// Sets itself up: a collider if there is none, a Rigidbody, and a grab
// interactable that falls when released. It sits perfectly still - kinematic,
// riding the ship - until grabbed; after that, gravity.
//
// Once it lands it pins itself again, or the rocking deck would slide it about
// forever. Everything goes back where it started on the way to the main menu.
public class GrabbableProp : MonoBehaviour
{
    [Tooltip("Optional. An empty child placed where the hand should hold it - a mug by its handle. Empty: it is held by its own pivot.")]
    public Transform grabPoint;

    [Tooltip("After it lands and stops, freeze it in place so the rocking deck does not slide it around.")]
    public bool pinWhenSettled = true;

    [Tooltip("Seconds it has to be still before it is pinned.")]
    public float settleTime = 1f;

    [Tooltip("Sent home straight away if it falls this far below where it started - over the side, through the deck.")]
    public float returnIfLostBelow = 15f;

    private static readonly List<GrabbableProp> All = new();

    private PropGrabInteractable grab;
    private Rigidbody body;

    private Transform homeParent;
    private Vector3 homeLocalPosition;
    private Quaternion homeLocalRotation;
    private float homeWorldY;
    private float stillFor;

    private void Awake()
    {
        All.Add(this);

        homeParent = transform.parent;
        homeLocalPosition = transform.localPosition;
        homeLocalRotation = transform.localRotation;
        homeWorldY = transform.position.y;

        EnsureCollider();

        // Unity's == null, not a C# pattern match - in the editor GetComponent
        // can hand back a "fake null" object that a pattern match accepts
        XRBaseInteractable existing = GetComponent<XRBaseInteractable>();

        if (existing != null && !(existing is PropGrabInteractable))
        {
            Debug.LogWarning(
                $"{name}: already has a {existing.GetType().Name}. Remove it - GrabbableProp adds its own, " +
                "which is the one that makes the prop fall when released.", this);
        }

        // Collider first: the interactable collects its colliders when added
        grab = GetComponent<PropGrabInteractable>();
        if (grab == null) grab = gameObject.AddComponent<PropGrabInteractable>();

        if (grabPoint != null)
            grab.attachTransform = grabPoint;

        body = GetComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = true;
    }

    private void OnDestroy()
    {
        All.Remove(this);
    }

#if UNITY_EDITOR
    // A dynamic Rigidbody cannot use a concave MeshCollider, so make them
    // convex HERE, in the editor, where it is saved with the scene and baked
    // into the build. Doing it at runtime instead forces Unity to rebuild the
    // collider shape on the device, which needs the mesh marked Read/Write - and
    // without that the collider silently ends up empty, with nothing to grab.
    // That was why props worked in the editor and not in the build.
    private void OnValidate()
    {
        if (Application.isPlaying) return;

        // A tick later: changing physics settings from inside OnValidate
        // itself makes Unity complain
        UnityEditor.EditorApplication.delayCall += MakeCollidersConvex;
    }

    private void MakeCollidersConvex()
    {
        if (this == null) return;

        foreach (MeshCollider mesh in GetComponentsInChildren<MeshCollider>(true))
        {
            if (mesh.isTrigger || mesh.convex) continue;

            mesh.convex = true;
            UnityEditor.EditorUtility.SetDirty(mesh);
        }
    }
#endif

    // At runtime nothing is rebuilt. With no usable solid collider, add a box -
    // a primitive needs no cooking, so it works in any build
    private void EnsureCollider()
    {
        bool solid = false;

        foreach (Collider col in GetComponentsInChildren<Collider>(true))
        {
            if (col.isTrigger) continue;

            // Missed by OnValidate (scene not saved since): switch it off
            // rather than let a dynamic body meet a concave mesh
            if (col is MeshCollider mesh && !mesh.convex)
            {
                Debug.LogWarning(
                    $"{name}: concave Mesh Collider - using a box instead. Open the scene in the " +
                    "editor and save it, and GrabbableProp will make it convex there.", this);
                mesh.enabled = false;
                continue;
            }

            solid = true;
        }

        // Sizes itself to the mesh on this object
        if (!solid) gameObject.AddComponent<BoxCollider>();
    }

    private void Update()
    {
        if (grab == null || body == null || grab.isSelected || body.isKinematic) return;

        if (transform.position.y < homeWorldY - returnIfLostBelow)
        {
            GoHome();
            return;
        }

        if (!pinWhenSettled) return;

        bool still = body.linearVelocity.sqrMagnitude < 0.01f && body.angularVelocity.sqrMagnitude < 0.05f;
        stillFor = still ? stillFor + Time.deltaTime : 0f;

        if (stillFor >= settleTime)
        {
            body.isKinematic = true;
            stillFor = 0f;

            // XRI parks a dropped object back under its old parent, but make
            // sure it rides the ship now that it is pinned
            if (transform.parent == null) ShipRoot.Attach(gameObject);
        }
    }

    // On the way back to the rowboat
    public static void ResetAll()
    {
        for (int i = All.Count - 1; i >= 0; i--)
        {
            if (All[i] != null) All[i].GoHome();
        }
    }

    public void GoHome()
    {
        // Out of the hand first, or the interactor keeps a hold on it
        if (grab != null && grab.isSelected && grab.interactionManager != null)
            grab.interactionManager.CancelInteractableSelection((IXRSelectInteractable)grab);

        if (body != null && !body.isKinematic)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }

        if (body != null) body.isKinematic = true;

        transform.SetParent(homeParent, false);
        transform.localPosition = homeLocalPosition;
        transform.localRotation = homeLocalRotation;

        stillFor = 0f;
    }

    // Statics survive "enter play mode without domain reload", so wipe them
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        All.Clear();
    }
}
