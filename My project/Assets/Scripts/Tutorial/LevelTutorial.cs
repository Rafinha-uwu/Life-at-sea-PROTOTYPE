using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit;

// Runs one arrow at a time through an ordered list of steps: put the arrow at
// an anchor, wait for the player to do the thing, move on. When the list runs
// out the arrow goes away and does not come back.
//
// Put one on each level container. Levels are switched on with SetActive, so
// OnEnable is the level starting - no wiring back to GAME needed.
public class LevelTutorial : MonoBehaviour
{
    public enum Condition
    {
        [Tooltip("Any of the listed interactables is picked up.")]
        Grab,

        [Tooltip("The player's head gets within radius of the anchor.")]
        Proximity,

        [Tooltip("The cannon reaches a given phase.")]
        CannonPhase,

        [Tooltip("Something else calls Advance() - a UnityEvent on another script.")]
        Manual,
    }

    [Serializable]
    public class Step
    {
        [Tooltip("Only a note to yourself - shows in the Console when the step starts.")]
        public string label = "step";

        [Tooltip("Where the arrow goes. Its rotation is used too, so aim this object the way the arrow should point.")]
        public Transform anchor;

        public Condition completeWhen = Condition.Grab;

        [Header("Grab")]
        [Tooltip("Any one of these completes the step.")]
        public XRBaseInteractable[] grabAny;

        [Tooltip("Optional: every interactable under here also counts. Use for the ladder, rather than dragging in every rung.")]
        public Transform grabAnythingUnder;

        [Header("Proximity")]
        public float radius = 1.5f;

        [Header("Cannon Phase")]
        public CannonLoader cannon;
        public CannonLoader.CannonPhase reachPhase = CannonLoader.CannonPhase.Powder;

        [Header("Timing")]
        [Tooltip("The arrow stays at least this long, so a step whose condition is already true does not blink out.")]
        public float minimumTime = 0.4f;

        [Header("Events")]
        public UnityEvent onShown;
        public UnityEvent onCompleted;

        [NonSerialized] public XRBaseInteractable[] resolvedGrabs;
    }

    [Header("Arrow")]
    public GameObject arrowPrefab;

    [Header("Start")]
    [Tooltip("Begin when this object is switched on, which is when the level starts.")]
    public bool runOnEnable = true;

    [Tooltip("Seconds to wait before the first arrow. L4 wants this long enough for the captain's briefing.")]
    public float startDelay = 0f;

    public Step[] steps;

    private GameObject arrow;
    private Transform playerCamera;
    private int index = -1;
    private float shownFor;
    private bool running;
    private bool grabbed;

    // Kept so the exact same listeners can be taken off again
    private readonly List<XRBaseInteractable> listening = new();

    private void OnEnable()
    {
        if (playerCamera == null && Camera.main != null)
            playerCamera = Camera.main.transform;

        if (runOnEnable) Begin();
    }

    private void OnDisable()
    {
        running = false;
        index = -1;
        StopListening();
        ClearArrow();
    }

    // Also callable from a UnityEvent, for levels that should not start the
    // tutorial the instant the container switches on.
    public void Begin()
    {
        StopAllCoroutines();

        if (startDelay > 0f) StartCoroutine(BeginAfterDelay());
        else StartFrom(0);
    }

    private IEnumerator BeginAfterDelay()
    {
        yield return new WaitForSeconds(startDelay);
        StartFrom(0);
    }

    // Hooked to a UnityEvent for Manual steps - the spice game's onCorrectItem,
    // for instance. Ignored unless the current step is actually waiting on it.
    public void Advance()
    {
        if (!running || index < 0 || index >= steps.Length) return;
        if (steps[index].completeWhen != Condition.Manual) return;

        CompleteStep();
    }

    // Stops the tutorial wherever it is and takes the arrow away
    public void StopTutorial()
    {
        running = false;
        index = -1;
        StopListening();
        ClearArrow();
    }

    private void StartFrom(int first)
    {
        running = true;
        index = first - 1;
        CompleteStep();
    }

    private void CompleteStep()
    {
        StopListening();

        if (index >= 0 && index < steps.Length)
            steps[index].onCompleted.Invoke();

        index++;

        if (index >= steps.Length)
        {
            StopTutorial();
            return;
        }

        Step step = steps[index];
        shownFor = 0f;

        ResolveGrabs(step);
        PlaceArrow(step.anchor);

        step.onShown.Invoke();
    }

    private void Update()
    {
        if (!running || index < 0 || index >= steps.Length) return;

        shownFor += Time.deltaTime;

        Step step = steps[index];

        if (shownFor < step.minimumTime) return;

        if (IsSatisfied(step))
            CompleteStep();
    }

    private bool IsSatisfied(Step step)
    {
        switch (step.completeWhen)
        {
            case Condition.Grab:
                // Set by the selectEntered listener, not polled. The piles hand
                // the player a freshly spawned item and drop the pile's own
                // selection in the same frame, so isSelected is never seen true.
                return grabbed;

            case Condition.Proximity:
                if (step.anchor == null || playerCamera == null) return false;
                return Vector3.Distance(playerCamera.position, step.anchor.position) <= step.radius;

            case Condition.CannonPhase:
                return step.cannon != null && step.cannon.currentPhase == step.reachPhase;

            default:
                return false;
        }
    }

    // Built once per step rather than every frame - GetComponentsInChildren
    // allocates, and this runs in Update
    private void ResolveGrabs(Step step)
    {
        if (step.completeWhen != Condition.Grab)
        {
            step.resolvedGrabs = Array.Empty<XRBaseInteractable>();
            return;
        }

        List<XRBaseInteractable> found = new();

        if (step.grabAny != null)
        {
            foreach (XRBaseInteractable grab in step.grabAny)
                if (grab != null) found.Add(grab);
        }

        if (step.grabAnythingUnder != null)
        {
            foreach (XRBaseInteractable grab in
                     step.grabAnythingUnder.GetComponentsInChildren<XRBaseInteractable>(true))
            {
                if (grab != null && !found.Contains(grab)) found.Add(grab);
            }
        }

        if (found.Count == 0)
        {
            Debug.LogWarning(
                $"{name}: tutorial step '{step.label}' waits for a grab but has nothing to watch. " +
                "It will never finish - fill Grab Any, or point Grab Anything Under at a parent.", this);
        }

        step.resolvedGrabs = found.ToArray();

        foreach (XRBaseInteractable grab in step.resolvedGrabs)
        {
            grab.selectEntered.AddListener(OnAnyGrabbed);
            listening.Add(grab);
        }
    }

    private void OnAnyGrabbed(SelectEnterEventArgs args)
    {
        grabbed = true;
    }

    private void StopListening()
    {
        foreach (XRBaseInteractable grab in listening)
        {
            if (grab != null) grab.selectEntered.RemoveListener(OnAnyGrabbed);
        }

        listening.Clear();
        grabbed = false;
    }

    private void PlaceArrow(Transform anchor)
    {
        if (anchor == null)
        {
            Debug.LogWarning($"{name}: tutorial step '{steps[index].label}' has no anchor.", this);
            ClearArrow();
            return;
        }

        if (arrow == null)
        {
            if (arrowPrefab == null) return;
            arrow = Instantiate(arrowPrefab);
        }

        // Parent to the anchor, so it rides the ship without any extra thought
        arrow.transform.SetParent(anchor, false);
        arrow.transform.localPosition = Vector3.zero;
        arrow.transform.localRotation = Quaternion.identity;
        arrow.SetActive(true);
    }

    private void ClearArrow()
    {
        if (arrow == null) return;

        Destroy(arrow);
        arrow = null;
    }
}
