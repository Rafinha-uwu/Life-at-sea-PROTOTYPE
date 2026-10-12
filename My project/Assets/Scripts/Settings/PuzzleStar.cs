using TMPro;
using UnityEngine;
using UnityEngine.UI;

// One star plus the best time, sitting on a puzzle button. Hidden until that
// puzzle has been finished at least once. Drop one on the puzzles page and
// another on the ship's level-select entry for the same puzzle - they read the
// same record, so they always agree.
public class PuzzleStar : MonoBehaviour
{
    [Range(1, PuzzleRecords.PuzzleCount)]
    public int puzzle = 1;

    public PuzzleRatings ratings;

    [Header("Bits to drive")]
    [Tooltip("Shown and hidden as a whole once the puzzle is finished. Must NOT be this object. Leave empty to drive the star and label directly instead.")]
    public GameObject shownWhenEarned;

    [Tooltip("The star graphic. Its colour comes from the time.")]
    public Graphic star;

    [Tooltip("Best time, shown next to the star.")]
    public TMP_Text timeLabel;

    public bool showTenths = true;

    private void Awake()
    {
        // Hiding the object this component lives on - or anything above it -
        // means OnEnable can never run again, so the star could never come
        // back once hidden. Ignore that wiring rather than die quietly.
        if (shownWhenEarned != null && transform.IsChildOf(shownWhenEarned.transform))
        {
            Debug.LogWarning(
                $"{name}: 'Shown When Earned' is this object or a parent of it, so the star " +
                "could never reappear. Hiding the graphics instead - point it at a child " +
                "container holding the star and the label.", this);

            shownWhenEarned = null;
        }
    }

    private void OnEnable()
    {
        PuzzleRecords.Changed += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        PuzzleRecords.Changed -= Refresh;
    }

    private void Refresh()
    {
        bool earned = PuzzleRecords.Has(puzzle);

        if (shownWhenEarned != null)
        {
            shownWhenEarned.SetActive(earned);
        }
        else
        {
            // Disabling the Graphic rather than the object, so this component
            // keeps running and can bring them back when the record lands.
            if (star != null) star.enabled = earned;
            if (timeLabel != null) timeLabel.enabled = earned;
        }

        if (!earned) return;

        float best = PuzzleRecords.GetBest(puzzle);

        if (star != null && ratings != null)
            star.color = ratings.ColorFor(puzzle, best);

        if (timeLabel != null)
            timeLabel.text = PuzzleRecords.Format(best, showTenths);
    }
}
