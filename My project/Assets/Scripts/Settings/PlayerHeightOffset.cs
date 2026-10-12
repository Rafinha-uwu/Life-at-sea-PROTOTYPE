using Unity.XR.CoreUtils;
using UnityEngine;

// Trims the player's eye height. Goes on the XR Origin (Player).
[RequireComponent(typeof(XROrigin))]
public class PlayerHeightOffset : MonoBehaviour
{
    [Tooltip("Camera Offset local Y with no adjustment. Leave 0 for Floor tracking.")]
    public float baseHeight = 0f;

    private XROrigin origin;
    private Transform cameraOffset;

    // There is exactly one rig and this is a global setting, so
    // attach ourselves rather than rely on remembering to add it
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoAttach()
    {
        XROrigin rig = FindAnyObjectByType<XROrigin>(FindObjectsInactive.Include);

        if (rig == null || rig.GetComponent<PlayerHeightOffset>() != null)
            return;

        rig.gameObject.AddComponent<PlayerHeightOffset>();
    }

    private void Awake()
    {
        origin = GetComponent<XROrigin>();

        if (origin.CameraFloorOffsetObject != null)
            cameraOffset = origin.CameraFloorOffsetObject.transform;
    }

    private void OnEnable()
    {
        GameSettings.HeightChanged += Apply;
        Apply();
    }

    private void OnDisable()
    {
        GameSettings.HeightChanged -= Apply;
    }

    // XROrigin rewrites this offset whenever tracking re-inits,
    // so watch the transform itself rather than just the setting
    private void LateUpdate()
    {
        if (cameraOffset == null) return;

        float wanted = baseHeight + GameSettings.HeightOffset;

        if (!Mathf.Approximately(cameraOffset.localPosition.y, wanted))
            Apply();
    }

    private void Apply()
    {
        if (cameraOffset == null) return;

        Vector3 local = cameraOffset.localPosition;
        local.y = baseHeight + GameSettings.HeightOffset;
        cameraOffset.localPosition = local;
    }
}
