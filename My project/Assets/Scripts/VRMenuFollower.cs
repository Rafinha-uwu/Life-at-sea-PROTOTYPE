using UnityEngine;

public class VRMenuFollower : MonoBehaviour
{
    [Header("References")]
    public Transform playerCamera;

    [Header("Positioning")]
    public float distanceFromPlayer = 2f;
    public float heightOffset = -0.2f;

    [Header("Recentering")]
    public float recenterAngle = 45f;
    public float moveSpeed = 5f;

    // Kept relative to the parent, not the world. Under the rocking ship that
    // means the spot moves with the deck - a world-space target stayed put
    // while the player rocked past it, so the menu seemed to float away.
    private Vector3 targetLocal;

    void OnEnable()
    {
        if (playerCamera == null && Camera.main != null)
            playerCamera = Camera.main.transform;

        SnapInFrontOfPlayer();
    }

    void Update()
    {
        if (playerCamera == null) return;

        FacePlayer();

        Vector3 playerForward = Flatten(playerCamera.forward);
        Vector3 toMenu = Flatten(transform.position - playerCamera.position);

        if (Vector3.Angle(playerForward, toMenu) > recenterAngle)
            SetTarget(InFrontOfPlayer(playerForward));

        transform.position = Vector3.Lerp(
            transform.position, TargetWorld, moveSpeed * Time.unscaledDeltaTime);
    }

    public void SnapInFrontOfPlayer()
    {
        if (playerCamera == null) return;

        SetTarget(InFrontOfPlayer(Flatten(playerCamera.forward)));
        transform.position = TargetWorld;
    }

    private void SetTarget(Vector3 world)
    {
        targetLocal = transform.parent != null ? transform.parent.InverseTransformPoint(world) : world;
    }

    private Vector3 TargetWorld =>
        transform.parent != null ? transform.parent.TransformPoint(targetLocal) : targetLocal;

    private void FacePlayer()
    {
        Vector3 lookPos = playerCamera.position;
        lookPos.y = transform.position.y;

        if (lookPos == transform.position) return;

        transform.LookAt(lookPos);
        transform.Rotate(0f, 180f, 0f);
    }

    private Vector3 InFrontOfPlayer(Vector3 forward)
    {
        return playerCamera.position + forward * distanceFromPlayer + Vector3.up * heightOffset;
    }

    private static Vector3 Flatten(Vector3 v)
    {
        v.y = 0f;
        return v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.forward;
    }
}
