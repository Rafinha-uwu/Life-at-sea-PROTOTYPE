using UnityEngine;

/// <summary>
/// Drives fog, sun light and ambient from the sun's elevation so the
/// warm daytime look and the night look both stay coherent. Cheap:
/// nothing is touched unless the sun has actually moved.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class SeaAtmosphere : MonoBehaviour
{
    [Header("Sun")]
    [SerializeField] Light sun;

    [Header("Day")]
    [SerializeField] Color dayFogColor = new Color(0.80f, 0.70f, 0.56f);
    [SerializeField, Range(0f, 0.1f)] float dayFogDensity = 0.012f;
    [SerializeField] Color daySunColor = new Color(1f, 0.87f, 0.68f);
    [SerializeField, Min(0f)] float daySunIntensity = 3.2f;
    [SerializeField, Range(0f, 8f)] float dayAmbientIntensity = 0.85f;

    [Header("Night")]
    [SerializeField] Color nightFogColor = new Color(0.10f, 0.11f, 0.15f);
    [SerializeField, Range(0f, 0.1f)] float nightFogDensity = 0.02f;
    [SerializeField] Color nightSunColor = new Color(0.55f, 0.62f, 0.80f);
    [SerializeField, Min(0f)] float nightSunIntensity = 0.35f;
    [SerializeField, Range(0f, 8f)] float nightAmbientIntensity = 0.3f;

    [Header("Blend")]
    [Tooltip("Sun elevation (dot with up) where the night look is fully in.")]
    [SerializeField, Range(-1f, 1f)] float nightBelow = -0.08f;
    [Tooltip("Sun elevation (dot with up) where the day look is fully in.")]
    [SerializeField, Range(-1f, 1f)] float dayAbove = 0.25f;

    float lastT = float.NaN;

    void OnEnable()
    {
        lastT = float.NaN;
        Apply();
    }

    void LateUpdate() => Apply();

    void OnValidate()
    {
        lastT = float.NaN;
        if (dayAbove <= nightBelow) dayAbove = nightBelow + 0.01f;
    }

    void Apply()
    {
        if (sun == null) sun = RenderSettings.sun;
        if (sun == null) return;

        float elevation = Vector3.Dot(-sun.transform.forward, Vector3.up);
        float t = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(nightBelow, dayAbove, elevation));

        if (!float.IsNaN(lastT) && Mathf.Abs(t - lastT) < 0.001f) return;
        lastT = t;

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = Color.Lerp(nightFogColor, dayFogColor, t);
        RenderSettings.fogDensity = Mathf.Lerp(nightFogDensity, dayFogDensity, t);
        RenderSettings.ambientIntensity = Mathf.Lerp(nightAmbientIntensity, dayAmbientIntensity, t);

        sun.color = Color.Lerp(nightSunColor, daySunColor, t);
        sun.intensity = Mathf.Lerp(nightSunIntensity, daySunIntensity, t);
    }
}
