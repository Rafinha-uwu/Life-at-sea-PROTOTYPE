using UnityEngine;

// Scales a text block and its backing panel together.
// Put it on the parent that holds both, not on the text itself.
public class ScalableText : MonoBehaviour
{
    [Tooltip("What to scale. Empty = this object.")]
    public Transform target;

    [Tooltip("How much of the setting to apply. 0.5 = half as much growth.")]
    [Range(0f, 1f)] public float strength = 1f;

    private Vector3 baseScale;

    private void Awake()
    {
        if (target == null) target = transform;
        baseScale = target.localScale;
    }

    private void OnEnable()
    {
        GameSettings.TextScaleChanged += Apply;
        Apply();
    }

    private void OnDisable()
    {
        GameSettings.TextScaleChanged -= Apply;
    }

    private void Apply()
    {
        if (target == null) return;

        float factor = Mathf.LerpUnclamped(1f, GameSettings.TextScale, strength);
        target.localScale = baseScale * factor;
    }
}
