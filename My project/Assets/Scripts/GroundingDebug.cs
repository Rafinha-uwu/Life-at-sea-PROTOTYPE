using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Gravity;

// Temporary diagnostic for the walk-on-air bug.
// Replicates GravityProvider's own ground probe and names what it hits.
public class GroundingDebug : MonoBehaviour
{
    [Tooltip("Optional world-space text so it can be read on device.")]
    public TMP_Text readout;

    public bool logToConsole = false;

    private XROrigin origin;
    private CharacterController controller;
    private GravityProvider gravity;

    private readonly RaycastHit[] hits = new RaycastHit[16];

    // Grounded flapping on a rocking deck shows up here
    private bool wasGrounded;
    private int flickers;
    private float flickerWindow;
    private int flickersPerSecond;

    private void Awake()
    {
        origin = GetComponent<XROrigin>();
        controller = GetComponent<CharacterController>();
        gravity = GetComponentInChildren<GravityProvider>(true);
    }

    private void Update()
    {
        if (origin == null || gravity == null) return;

        float camHeight = origin.CameraInOriginSpaceHeight;
        float offsetY = origin.CameraFloorOffsetObject != null
            ? origin.CameraFloorOffsetObject.transform.localPosition.y
            : -1f;

        // Same probe GravityProvider uses
        float scale = origin.Origin.transform.lossyScale.y;
        Vector3 up = gravity.useLocalSpaceGravity ? origin.Origin.transform.up : Vector3.up;

        Vector3 ccCenter = controller != null ? controller.bounds.center : transform.position;
        Vector3 head = origin.Camera != null ? origin.Camera.transform.position : transform.position;
        Vector3 castOrigin = new Vector3(ccCenter.x, head.y, ccCenter.z);

        float radius = gravity.sphereCastRadius * scale;
        float distance = (camHeight + gravity.sphereCastDistanceBuffer) * scale;

        int count = Physics.SphereCastNonAlloc(castOrigin, radius, -up, hits, distance,
            gravity.sphereCastLayerMask, gravity.sphereCastTriggerInteraction);

        string holder = "nothing";
        float lateral = -1f;

        if (count > 0)
        {
            RaycastHit best = hits[0];
            for (int i = 1; i < count; i++)
            {
                if (hits[i].distance < best.distance) best = hits[i];
            }

            // How far off the vertical axis the contact sits
            Vector3 toHit = best.point - castOrigin;
            lateral = Vector3.ProjectOnPlane(toHit, up).magnitude;

            holder = $"{best.collider.name} L{best.collider.gameObject.layer} " +
                     $"d{best.distance:F2} lat{lateral:F2}";
        }

        // Thin straight-down probe for comparison
        float drop = -1f;
        if (Physics.Raycast(transform.position + up * 0.1f, -up,
                out RaycastHit straight, 30f, gravity.sphereCastLayerMask,
                QueryTriggerInteraction.Ignore))
        {
            drop = straight.distance - 0.1f;
        }

        if (gravity.isGrounded != wasGrounded)
        {
            wasGrounded = gravity.isGrounded;
            flickers++;
        }

        flickerWindow += Time.deltaTime;
        if (flickerWindow >= 1f)
        {
            flickersPerSecond = flickers;
            flickers = 0;
            flickerWindow = 0f;
        }

        string text =
            $"camH {camHeight:F2} offY {offsetY:F2} mode {origin.CurrentTrackingOriginMode}\n" +
            $"radius {radius:F2} dist {distance:F2}\n" +
            $"gravGround {gravity.isGrounded}  ccGround " +
            $"{(controller != null && controller.isGrounded)}\n" +
            $"ground flips/sec {flickersPerSecond}\n" +
            $"straight drop {drop:F2}\n" +
            $"probe hits {count}: {holder}";

        if (readout != null) readout.text = text;
        if (logToConsole) Debug.Log(text);
    }
}
