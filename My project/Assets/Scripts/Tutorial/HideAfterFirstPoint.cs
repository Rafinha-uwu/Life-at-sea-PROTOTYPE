using UnityEngine;

// The "press the trigger to point" hint at the top of the mast. Hides the
// first time the player actually points up there, and comes back the next
// time the level starts.
//
// Lives under the L1 container: switching the level on is what re-arms it.
// The hint itself must be a CHILD of this object - if this object hid itself,
// switching the level back on would not bring it back.
public class HideAfterFirstPoint : MonoBehaviour
{
    [Tooltip("The hint to show and hide. Must be a child of this object, not this object.")]
    public GameObject hint;

    private bool done;

    private void OnEnable()
    {
        if (hint == null || transform.IsChildOf(hint.transform))
        {
            Debug.LogWarning(
                $"{name}: 'Hint' must be a child of this object. As this object or a parent, " +
                "it could never come back after hiding.", this);
            enabled = false;
            return;
        }

        done = false;
        hint.SetActive(true);
    }

    private void Update()
    {
        if (done) return;

        // Pointing only counts once the nest has enabled it, so a trigger
        // press on the way up does not clear the hint early. Either hand counts.
        if (!PointerReader.AnyPointing) return;

        done = true;
        hint.SetActive(false);
    }
}
