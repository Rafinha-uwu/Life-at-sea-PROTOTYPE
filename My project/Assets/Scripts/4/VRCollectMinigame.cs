using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class VRCollectMinigame : MonoBehaviour
{
    [Header("Possible Items")]
    [SerializeField] private List<CollectItemData> possibleItems;

    [Header("UI")]
    [SerializeField] private TMP_Text targetText;
    [SerializeField] private TMP_Text timerText;

    [Tooltip("The order board. Hidden the moment the last spice is delivered, and shown again for the next run. Leave empty to keep the old \"Well done, sailor!\" message instead.")]
    [SerializeField] private GameObject board;

    [Header("Rules")]
    [SerializeField] private int winsNeeded = 3;
    [Tooltip("Seconds for each delivery in order - the clock gets tighter as you go. Runs out of entries: the last one repeats. Empty: Round Time for every delivery.")]
    [SerializeField] private float[] roundTimes = { 60f, 45f, 30f };
    [SerializeField] private float roundTime = 30f;
    [SerializeField] private float feedbackTime = 1.5f;

    [Header("Restart")]
    [Tooltip("Shown when the clock runs out; pressing it restarts the orders. Leave empty to restart automatically after Feedback Time, as before.")]
    [SerializeField] private GameObject restartButton;

    [Header("Delivered Items")]
    [Tooltip("Optional. Leave empty to leave items where they land.")]
    [SerializeField] private Transform deliveredSpot;
    [SerializeField] private float deliveredSettleTime = 1.5f;

    [Header("Events")]
    [SerializeField] private UnityEvent onGameWon;
    [SerializeField] private UnityEvent onGameFailed;
    [SerializeField] private UnityEvent onCorrectItem;
    [SerializeField] private UnityEvent onWrongItem;

    private readonly List<CollectItemData> remainingItems = new();

    // One drop is one answer. A spice has more than one collider (Canela has
    // its root box and a child cube), so a single drop enters the zone twice
    // in the same step: the first counted as right, started the next order,
    // and the second was then judged against THAT order - wrong, every time
    private readonly HashSet<CollectItemData> delivered = new();
    private readonly Dictionary<CollectItemData, float> lastJudged = new();
    private const float JudgeCooldown = 1f;

    private CollectItemData currentTarget;
    private int currentScore;
    private float timer;
    private bool gameRunning;

    private Coroutine roundRoutine;
    private Typewriter targetTypewriter;

    public string CurrentTargetID => currentTarget != null ? currentTarget.ItemID : "";

    private void Awake()
    {
        // Shares the sailor bubble, so reuse its typewriter
        if (targetText != null)
        {
            targetTypewriter = targetText.GetComponent<Typewriter>();

            if (targetTypewriter == null)
                targetTypewriter = targetText.gameObject.AddComponent<Typewriter>();
        }

        // Hiding the game's own object would switch off the game and the drop zone with it
        if (board != null && transform.IsChildOf(board.transform))
        {
            Debug.LogWarning(
                $"{name}: 'Board' is this object or a parent of it, so hiding it would turn the spice " +
                "game off. Point it at the UI board itself.", this);
            board = null;
        }

        // No clock until a round is actually running
        ShowTimer(false);
        ShowRestart(false);
    }

    // The timer never types - it changes every frame
    private void WriteTarget(string text)
    {
        if (targetTypewriter != null) targetTypewriter.Show(text);
        else if (targetText != null) targetText.text = text;
    }

    private void ShowTimer(bool visible)
    {
        if (timerText == null) return;

        timerText.enabled = visible;
        if (!visible) timerText.text = "";
    }

    public void StartGame()
    {
        StopRound();
        ShowRestart(false);
        ShowBoard();

        currentScore = 0;
        delivered.Clear();
        lastJudged.Clear();

        remainingItems.Clear();
        remainingItems.AddRange(possibleItems);

        StartNextRound();
    }

    public void StopGame()
    {
        StopRound();
        ShowRestart(false);
        ShowBoard();

        gameRunning = false;
        currentTarget = null;

        if (targetTypewriter != null) targetTypewriter.Clear();
        else if (targetText != null) targetText.text = "";

        ShowTimer(false);
    }

    // Docked for bumping into a sailor. Ignored unless a round is actually
    // running, so a shove in the hub or between rounds costs nothing.
    public void LoseTime(float seconds)
    {
        if (!gameRunning || seconds <= 0f) return;

        timer = Mathf.Max(timer - seconds, 0.01f);

        if (timerText != null)
            timerText.text = Mathf.CeilToInt(timer).ToString();
    }

    public bool IsRunning => gameRunning;

    public void TrySubmitItem(CollectItemData submittedItem)
    {
        if (!gameRunning || currentTarget == null || submittedItem == null)
            return;

        // Already on the table, or the same drop seen through its other collider
        // or bouncing on the edge of the zone
        if (delivered.Contains(submittedItem)) return;

        if (lastJudged.TryGetValue(submittedItem, out float judgedAt) && Time.time - judgedAt < JudgeCooldown)
            return;

        lastJudged[submittedItem] = Time.time;

        if (submittedItem.ItemID == currentTarget.ItemID)
            CorrectItem(submittedItem);
        else
            onWrongItem?.Invoke();
    }

    private void StartNextRound()
    {
        StopRound();

        if (currentScore >= winsNeeded)
        {
            WinGame();
            return;
        }

        if (remainingItems.Count == 0)
        {
            Debug.LogWarning("Not enough items for winsNeeded rounds.", this);
            WinGame();
            return;
        }

        int index = Random.Range(0, remainingItems.Count);

        currentTarget = remainingItems[index];
        remainingItems.RemoveAt(index);

        ShowTarget();

        timer = RoundTimeFor(currentScore);
        gameRunning = true;

        ShowTimer(true);

        roundRoutine = StartCoroutine(TimerRoutine());
    }

    // currentScore is the number already delivered, so it indexes this delivery
    private float RoundTimeFor(int delivery)
    {
        if (roundTimes == null || roundTimes.Length == 0) return roundTime;
        return roundTimes[Mathf.Clamp(delivery, 0, roundTimes.Length - 1)];
    }

    private void ShowTarget()
    {
        if (targetText == null) return;

        // Description only. Naming the spice gave the answer away, and the
        // score belongs on the board rather than in the captain's order.
        WriteTarget(currentTarget.DescriptionText);
    }

    private IEnumerator TimerRoutine()
    {
        while (timer > 0f)
        {
            timer -= Time.deltaTime;

            if (timerText != null)
                timerText.text = Mathf.CeilToInt(Mathf.Max(timer, 0f)).ToString();

            yield return null;
        }

        roundRoutine = null;
        FailGame();
    }

    private void CorrectItem(CollectItemData item)
    {
        StopRound();

        gameRunning = false;
        currentScore++;
        delivered.Add(item);

        onCorrectItem?.Invoke();

        Park(item.gameObject);

        StartNextRound();
    }

    // Delivered items stay on the table
    private void Park(GameObject item)
    {
        XRGrabInteractable grab = item.GetComponentInParent<XRGrabInteractable>();

        if (grab != null)
        {
            if (grab.isSelected && grab.interactionManager != null)
                grab.interactionManager.CancelInteractableSelection((IXRSelectInteractable)grab);

            grab.enabled = false;
            item = grab.gameObject;
        }

        if (deliveredSpot != null)
            item.transform.SetParent(deliveredSpot, true);

        StartCoroutine(FreezeWhenSettled(item));
    }

    // Let it land, then pin it
    private IEnumerator FreezeWhenSettled(GameObject item)
    {
        yield return new WaitForSeconds(deliveredSettleTime);

        if (item == null) yield break;

        Rigidbody rb = item.GetComponent<Rigidbody>();

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }
    }

    private void FailGame()
    {
        StopRound();

        gameRunning = false;

        onGameFailed?.Invoke();

        WriteTarget(GameSettings.Pick(
            "Acabou o tempo! Outra vez...", "Out of time! Again..."));
        ShowTimer(false);

        // Wait for the player to ask for another go, rather than throwing
        // them straight back in before they have read what went wrong
        if (restartButton != null)
        {
            ShowRestart(true);
            return;
        }

        roundRoutine = StartCoroutine(RestartAfterFeedback());
    }

    // Hooked to the restart button's On Click
    public void Restart()
    {
        ShowRestart(false);
        StartGame();
    }

    private void ShowRestart(bool visible)
    {
        if (restartButton != null) restartButton.SetActive(visible);
    }

    private IEnumerator RestartAfterFeedback()
    {
        yield return new WaitForSeconds(feedbackTime);

        roundRoutine = null;
        StartGame();
    }

    private void WinGame()
    {
        StopRound();

        gameRunning = false;
        currentTarget = null;

        ShowTimer(false);

        // Last spice is in: the board's job is done, so it goes
        if (board != null)
            board.SetActive(false);
        else
            WriteTarget(GameSettings.Pick("Boa, marujo!", "Well done, sailor!"));

        onGameWon?.Invoke();
    }

    // Back up for the next run. The board may sit inside the L4 container,
    // and switching the level back on does not revive a child that was
    // switched off by itself
    private void ShowBoard()
    {
        if (board != null) board.SetActive(true);
    }

    private void StopRound()
    {
        if (roundRoutine != null)
        {
            StopCoroutine(roundRoutine);
            roundRoutine = null;
        }
    }
}
