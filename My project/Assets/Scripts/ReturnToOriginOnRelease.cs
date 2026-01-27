using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

[RequireComponent(typeof(UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable))]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class ReturnToOriginOnRelease : MonoBehaviour
{
    [Header("Return Settings")]
    public float returnDelay = 2f;
    public float returnDuration = 0.75f;

    [Tooltip("Controls how the object moves back")]
    public AnimationCurve returnCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    private Vector3 originalPosition;
    private Quaternion originalRotation;

    private UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable grabInteractable;
    private Rigidbody rb;
    private Collider col;

    private Coroutine returnCoroutine;

    void Awake()
    {
        grabInteractable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();

        originalPosition = transform.position;
        originalRotation = transform.rotation;
    }

    void OnEnable()
    {
        grabInteractable.selectEntered.AddListener(OnGrabbed);
        grabInteractable.selectExited.AddListener(OnReleased);
    }

    void OnDisable()
    {
        grabInteractable.selectEntered.RemoveListener(OnGrabbed);
        grabInteractable.selectExited.RemoveListener(OnReleased);
    }

    private void OnGrabbed(SelectEnterEventArgs args)
    {
        if (returnCoroutine != null)
        {
            StopCoroutine(returnCoroutine);
            returnCoroutine = null;
        }

        col.enabled = true;
        rb.isKinematic = false;
    }

    private void OnReleased(SelectExitEventArgs args)
    {
        returnCoroutine = StartCoroutine(ReturnToOriginWithDelay());
    }

    private IEnumerator ReturnToOriginWithDelay()
    {
        // Wait before starting the return
        yield return new WaitForSeconds(returnDelay);

        col.enabled = false;
        rb.isKinematic = true;

        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;

        float elapsed = 0f;

        while (elapsed < returnDuration)
        {
            elapsed += Time.deltaTime;

            float normalizedTime = elapsed / returnDuration;
            float curvedTime = returnCurve.Evaluate(normalizedTime);

            transform.position = Vector3.Lerp(startPos, originalPosition, curvedTime);
            transform.rotation = Quaternion.Slerp(startRot, originalRotation, curvedTime);

            yield return null;
        }

        transform.position = originalPosition;
        transform.rotation = originalRotation;

        col.enabled = true;
        rb.isKinematic = false;
        returnCoroutine = null;
    }
}
