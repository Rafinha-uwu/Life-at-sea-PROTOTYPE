using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[RequireComponent(typeof(XRGrabInteractable))]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class ReturnToOriginOnRelease : MonoBehaviour
{
    [Header("Return Settings")]
    public float returnDelay = 2f;
    public float returnDuration = 0.75f;

    public AnimationCurve returnCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    // Local: rides the ship
    private Transform home;
    private Vector3 homeLocalPosition;
    private Quaternion homeLocalRotation;

    private XRGrabInteractable grabInteractable;
    private Rigidbody rb;
    private Collider col;

    private Coroutine returnCoroutine;

    void Awake()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();

        CaptureHome();
    }

    void Start()
    {
        // A spawned copy is parented onto the ship right after Instantiate,
        // which is after Awake - so re-read the home pose once it is hanging
        // in the right place, or it would return to a fixed world point while
        // the deck rocks away from under it.
        if (home == null)
            CaptureHome();
    }

    private void CaptureHome()
    {
        home = transform.parent;
        homeLocalPosition = transform.localPosition;
        homeLocalRotation = transform.localRotation;
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

        StopReturning();
        col.enabled = true;
    }

    private void OnGrabbed(SelectEnterEventArgs args)
    {
        StopReturning();

        col.enabled = true;
        rb.isKinematic = false;
    }

    private void OnReleased(SelectExitEventArgs args)
    {
        returnCoroutine = StartCoroutine(ReturnToOriginWithDelay());
    }

    private void StopReturning()
    {
        if (returnCoroutine != null)
        {
            StopCoroutine(returnCoroutine);
            returnCoroutine = null;
        }
    }

    private Vector3 HomePosition =>
        home != null ? home.TransformPoint(homeLocalPosition) : homeLocalPosition;

    private Quaternion HomeRotation =>
        home != null ? home.rotation * homeLocalRotation : homeLocalRotation;

    private IEnumerator ReturnToOriginWithDelay()
    {
        yield return new WaitForSeconds(returnDelay);

        col.enabled = false;
        rb.isKinematic = true;

        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;

        float elapsed = 0f;

        while (elapsed < returnDuration)
        {
            elapsed += Time.deltaTime;
            float t = returnCurve.Evaluate(elapsed / returnDuration);

            transform.position = Vector3.Lerp(startPos, HomePosition, t);
            transform.rotation = Quaternion.Slerp(startRot, HomeRotation, t);

            yield return null;
        }

        transform.SetPositionAndRotation(HomePosition, HomeRotation);

        col.enabled = true;
        rb.isKinematic = false;
        returnCoroutine = null;
    }
}
