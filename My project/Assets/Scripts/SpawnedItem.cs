using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// Added at runtime to anything a pile spawns, so a level
// can sweep up whatever the player left lying around.
public class SpawnedItem : MonoBehaviour
{
    private static readonly List<SpawnedItem> Spawned = new();

    private void Awake()
    {
        Spawned.Add(this);
    }

    private void OnDestroy()
    {
        Spawned.Remove(this);
    }

    public static void Mark(GameObject item)
    {
        if (item != null && item.GetComponent<SpawnedItem>() == null)
            item.AddComponent<SpawnedItem>();
    }

    public static void ClearAll()
    {
        for (int i = Spawned.Count - 1; i >= 0; i--)
        {
            SpawnedItem item = Spawned[i];
            if (item == null) continue;

            Release(item.gameObject);
            Destroy(item.gameObject);
        }

        Spawned.Clear();
    }

    // Let go before destroying, or the hand keeps a dead reference
    private static void Release(GameObject item)
    {
        XRGrabInteractable grab = item.GetComponent<XRGrabInteractable>();

        if (grab != null && grab.isSelected && grab.interactionManager != null)
            grab.interactionManager.CancelInteractableSelection((IXRSelectInteractable)grab);
    }
}
