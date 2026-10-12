using UnityEngine;
using System.Collections;

public class SunXRotator : MonoBehaviour
{
    [Header("Sun Positions")]
    public float dayXRotation;
    public float nightXRotation;

    [Header("Transition")]
    public float transitionSpeed = 10f;

    private Coroutine currentTransition;

    public void TransitionToDay()
    {
        StartTransition(dayXRotation);
    }

    public void TransitionToNight()
    {
        StartTransition(nightXRotation);
    }

    // Instant, for changes made while the screen is black - no one should
    // watch the sun swing across the sky under the fade
    public void SnapTo(float xRotation)
    {
        if (currentTransition != null)
        {
            StopCoroutine(currentTransition);
            currentTransition = null;
        }

        Vector3 euler = transform.eulerAngles;
        transform.eulerAngles = new Vector3(xRotation, euler.y, euler.z);
    }

    public void SnapToDay() => SnapTo(dayXRotation);

    public void ToggleDayNight()
    {
        float currentX = transform.eulerAngles.x;

        float toDay = Mathf.Abs(Mathf.DeltaAngle(currentX, dayXRotation));
        float toNight = Mathf.Abs(Mathf.DeltaAngle(currentX, nightXRotation));

        StartTransition(toDay < toNight ? nightXRotation : dayXRotation);
    }

    private void StartTransition(float targetX)
    {
        if (currentTransition != null)
            StopCoroutine(currentTransition);

        currentTransition = StartCoroutine(RotateSun(targetX));
    }

    private IEnumerator RotateSun(float targetX)
    {
        Vector3 euler = transform.eulerAngles;
        float currentX = euler.x;

        while (Mathf.Abs(Mathf.DeltaAngle(currentX, targetX)) > 0.01f)
        {
            currentX = Mathf.MoveTowardsAngle(
                currentX, targetX, transitionSpeed * Time.deltaTime);

            transform.eulerAngles = new Vector3(currentX, euler.y, euler.z);

            yield return null;
        }

        transform.eulerAngles = new Vector3(targetX, euler.y, euler.z);
        currentTransition = null;
    }
}
