using UnityEngine;

// Goes on the arrow prefab. Purely cosmetic - LevelTutorial decides where the
// arrow sits and when it exists. Everything here is optional; an arrow prefab
// with no TutorialArrow on it still works, it just sits perfectly still.
public class TutorialArrow : MonoBehaviour
{
    [Header("Bob")]
    [Tooltip("Metres it travels, up and down, along its own up axis.")]
    public float bobDistance = 0.08f;

    [Tooltip("Bobs per second.")]
    public float bobSpeed = 1.2f;

    [Header("Spin")]
    [Tooltip("Degrees per second about its own up axis. 0 for none.")]
    public float spinSpeed = 0f;

    [Header("Facing")]
    [Tooltip("Turn to face the player, keeping the anchor's tilt. Leave off for arrows whose direction is the point - one aimed up a mast, say.")]
    public bool facePlayer = false;

    private Vector3 restLocalPosition;
    private Quaternion restLocalRotation;
    private Transform playerCamera;
    private float phase;

    private void OnEnable()
    {
        restLocalPosition = transform.localPosition;
        restLocalRotation = transform.localRotation;

        // Random, so a row of arrows does not bob in lockstep
        phase = Random.value * Mathf.PI * 2f;

        if (playerCamera == null && Camera.main != null)
            playerCamera = Camera.main.transform;
    }

    private void LateUpdate()
    {
        float t = Time.time * bobSpeed * Mathf.PI * 2f + phase;

        transform.localPosition = restLocalPosition + Vector3.up * (Mathf.Sin(t) * bobDistance);

        if (spinSpeed != 0f)
            transform.localRotation = restLocalRotation * Quaternion.Euler(0f, Time.time * spinSpeed, 0f);

        if (facePlayer && playerCamera != null)
        {
            Vector3 away = transform.position - playerCamera.position;
            away.y = 0f;

            if (away.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(away.normalized, transform.parent != null ? transform.parent.up : Vector3.up);
        }
    }
}
