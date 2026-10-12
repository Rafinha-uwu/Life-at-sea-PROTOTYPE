using UnityEngine;

// Put this on the controller that should aim at info panels.
public class InfoPointer : MonoBehaviour
{
    [Header("Ray")]
    [Tooltip("Where the ray starts. Empty = this object.")]
    [SerializeField] private Transform rayOrigin;
    [SerializeField] private float maxDistance = 6f;
    [SerializeField] private LayerMask layerMask = ~0;
    [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Collide;

    [Header("Forgiveness")]
    [Tooltip("Fat ray, so small targets are easier to hit. 0 = thin ray.")]
    [SerializeField] private float rayRadius = 0.05f;

    [Header("Debug")]
    [Tooltip("Draws the ray and logs what it hits.")]
    [SerializeField] private bool debugRay = false;

    private readonly RaycastHit[] hits = new RaycastHit[16];
    private ProximityWorldUIPopup current;

    private void Awake()
    {
        if (rayOrigin == null)
            rayOrigin = transform;
    }

    private void Update()
    {
        ProximityWorldUIPopup found = Probe();

        if (found != current)
        {
            if (current != null) current.SetPointedAt(false);

            current = found;

            if (current != null) current.SetPointedAt(true);
        }

        if (debugRay)
        {
            Debug.DrawRay(rayOrigin.position, rayOrigin.forward * maxDistance,
                current != null ? Color.green : Color.red);
        }
    }

    private void OnDisable()
    {
        if (current != null)
        {
            current.SetPointedAt(false);
            current = null;
        }
    }

    // Nearest hit that maps to a panel, so railings don't block the target behind them
    private ProximityWorldUIPopup Probe()
    {
        int count = rayRadius > 0f
            ? Physics.SphereCastNonAlloc(rayOrigin.position, rayRadius, rayOrigin.forward,
                hits, maxDistance, layerMask, triggerInteraction)
            : Physics.RaycastNonAlloc(new Ray(rayOrigin.position, rayOrigin.forward),
                hits, maxDistance, layerMask, triggerInteraction);

        ProximityWorldUIPopup best = null;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            ProximityWorldUIPopup panel = ProximityWorldUIPopup.FindByCollider(hits[i].collider);

            if (panel == null || hits[i].distance >= bestDistance) continue;

            best = panel;
            bestDistance = hits[i].distance;
        }

        if (debugRay && count > 0 && best == null)
            Debug.Log($"InfoPointer hit {hits[0].collider.name} but it maps to no panel.");

        return best;
    }
}
