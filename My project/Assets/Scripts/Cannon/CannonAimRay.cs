using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals;

[RequireComponent(typeof(LineRenderer))]
public class CannonAimRay : MonoBehaviour
{
    public CannonLoader loader;
    public Transform firePoint;

    [Header("Trajectory")]
    [Tooltip("How many segments the arc is drawn with.")]
    public int maxPoints = 60;

    [Tooltip("Roughly how long each segment is, in metres.")]
    public float segmentLength = 2f;

    [Tooltip("Safety cap on flight time.")]
    public float maxFlightTime = 15f;

    [Header("Impact")]
    public LayerMask hitMask = ~0;

    [Tooltip("Match the cannonball's collider radius.")]
    public float projectileRadius = 0.8f;

    [Tooltip("Ignore collisions this early, so the barrel isn't hit.")]
    public float muzzleClearTime = 0.08f;

    [Tooltip("Optional marker placed where the ball will land.")]
    public GameObject impactMarker;

    private LineRenderer line;
    private Vector3[] points;

    void Awake()
    {
        line = GetComponent<LineRenderer>();
        line.useWorldSpace = true;
        points = new Vector3[Mathf.Max(maxPoints, 2)];

        StopXRFightingOverTheLine();
    }

    // XRInteractorLineVisual rewrites the same LineRenderer every
    // LateUpdate, so the trajectory never survives to be seen
    private void StopXRFightingOverTheLine()
    {
        XRInteractorLineVisual visual = GetComponent<XRInteractorLineVisual>();

        if (visual != null && visual.enabled)
        {
            visual.enabled = false;
            Debug.LogWarning(
                "Disabled XRInteractorLineVisual - it was overwriting the cannon " +
                "trajectory. Remove it and XRRayInteractor from this object.", this);
        }

        XRRayInteractor interactor = GetComponent<XRRayInteractor>();

        if (interactor != null && interactor.enabled)
        {
            Debug.LogWarning(
                "XRRayInteractor is still on the cannon aim line. It raycasts and can " +
                "hover things the barrel points at - remove it.", this);
        }
    }

    void OnDisable()
    {
        Hide();
    }

    void Update()
    {
        DrawTrajectory();
    }

    void DrawTrajectory()
    {
        float speed = loader != null ? loader.GetPower() : 0f;

        // No powder, no arc
        if (speed <= 0f || firePoint == null)
        {
            Hide();
            return;
        }

        if (points.Length != Mathf.Max(maxPoints, 2))
            points = new Vector3[Mathf.Max(maxPoints, 2)];

        Vector3 start = firePoint.position;
        Vector3 velocity = firePoint.forward * speed;

        // Constant arc length per segment, so range grows with powder
        float step = segmentLength / Mathf.Max(speed, 0.01f);

        int count = 1;
        points[0] = start;

        Vector3 previous = start;
        bool landed = false;
        Vector3 impact = Vector3.zero;

        for (int i = 1; i < points.Length; i++)
        {
            float t = i * step;
            if (t > maxFlightTime) break;

            Vector3 next = start + velocity * t + 0.5f * t * t * Physics.gravity;

            Vector3 segment = next - previous;
            float length = segment.magnitude;

            if (t > muzzleClearTime && length > 0.0001f &&
                Physics.SphereCast(previous, projectileRadius, segment / length,
                    out RaycastHit hit, length, hitMask, QueryTriggerInteraction.Ignore))
            {
                points[count++] = hit.point;
                impact = hit.point;
                landed = true;
                break;
            }

            points[count++] = next;
            previous = next;
        }

        line.enabled = true;
        line.positionCount = count;

        // Per-point, since the buffer is longer than the arc
        for (int i = 0; i < count; i++)
            line.SetPosition(i, points[i]);

        if (impactMarker != null)
        {
            impactMarker.SetActive(landed);

            if (landed)
                impactMarker.transform.position = impact;
        }
    }

    private void Hide()
    {
        if (line != null)
        {
            line.positionCount = 0;
            line.enabled = false;
        }

        if (impactMarker != null)
            impactMarker.SetActive(false);
    }
}
