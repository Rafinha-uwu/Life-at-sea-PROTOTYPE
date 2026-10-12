using UnityEngine;

// Anything spawned at runtime that should stay put on the deck has to live
// under the rocking root, or it slides around while the ship rolls under it.
// The root is whatever carries BoatWiggle - no wiring needed.
public static class ShipRoot
{
    private static Transform cached;

    public static Transform Transform
    {
        get
        {
            if (cached == null)
            {
                BoatWiggle wiggle = Object.FindFirstObjectByType<BoatWiggle>();
                if (wiggle != null) cached = wiggle.transform;
            }

            return cached;
        }
    }

    // Keeps the world pose it was spawned at, just re-hangs it on the ship.
    public static void Attach(GameObject spawned)
    {
        if (spawned != null)
            Attach(spawned.transform);
    }

    public static void Attach(Transform spawned)
    {
        Transform root = Transform;

        if (spawned != null && root != null && !spawned.IsChildOf(root))
            spawned.SetParent(root, true);
    }

    // For effects spawned at another object: follow whatever that object rides.
    public static void AttachBeside(GameObject spawned, Transform sibling)
    {
        if (spawned == null) return;

        if (sibling != null && sibling.parent != null)
            spawned.transform.SetParent(sibling.parent, true);
        else
            Attach(spawned);
    }

    // Statics survive "enter play mode without domain reload", so wipe them
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        cached = null;
    }
}
