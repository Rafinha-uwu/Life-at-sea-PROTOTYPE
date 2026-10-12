using System.Collections;
using UnityEngine;

// The sea sits higher while the start menu is up - the rowboat rides low in
// the water - and drops back to its authored level once the player is aboard.
public class OceanHeight : MonoBehaviour
{
    [Tooltip("The ocean transform to move. Leave empty to move this object.")]
    public Transform ocean;

    [Tooltip("How much higher the sea sits while the start menu is up, in metres. This is the number to tweak.")]
    public float riseOnStartMenu = 1.5f;

    [Tooltip("Seconds to slide between the two heights. 0 snaps - which is what you want while the screen is black.")]
    public float transitionTime = 0f;

    private Transform Target => ocean != null ? ocean : transform;

    private float shipHeight;
    private bool captured;
    private Coroutine sliding;

    private void Awake()
    {
        Capture();
    }

    public void RaiseForStartMenu() => MoveTo(Capture() + riseOnStartMenu);

    public void LowerForShip() => MoveTo(Capture());

    // GAME may ask for a height from its own Awake, and Awake order between
    // two components is not defined - so read the authored level on demand.
    private float Capture()
    {
        if (!captured)
        {
            shipHeight = Target.position.y;
            captured = true;
        }

        return shipHeight;
    }

    private void MoveTo(float height)
    {
        if (sliding != null)
        {
            StopCoroutine(sliding);
            sliding = null;
        }

        if (transitionTime <= 0f || !isActiveAndEnabled)
        {
            SetHeight(height);
            return;
        }

        sliding = StartCoroutine(Slide(height));
    }

    private IEnumerator Slide(float height)
    {
        float from = Target.position.y;
        float elapsed = 0f;

        while (elapsed < transitionTime)
        {
            elapsed += Time.deltaTime;
            SetHeight(Mathf.Lerp(from, height, elapsed / transitionTime));
            yield return null;
        }

        SetHeight(height);
        sliding = null;
    }

    private void SetHeight(float height)
    {
        Vector3 position = Target.position;
        position.y = height;
        Target.position = position;
    }
}
