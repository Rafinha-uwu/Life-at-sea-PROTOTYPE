using System;
using UnityEngine;

// The one place the star colours and the per-puzzle times live. An asset
// rather than a scene component, so every star on every page reads the same
// numbers and tuning them never touches the 7 MB scene.
[CreateAssetMenu(fileName = "PuzzleRatings", menuName = "Nau/Puzzle Ratings")]
public class PuzzleRatings : ScriptableObject
{
    [Serializable]
    public class Thresholds
    {
        [Tooltip("Just a label so the array is readable in the inspector.")]
        public string label = "Puzzle";

        [Tooltip("Seconds. Finish at or under this and the star takes the best colour.")]
        public float bestUnder = 60f;

        [Tooltip("Seconds. At or under this takes the middle colour. Anything slower takes the last one.")]
        public float goodUnder = 120f;
    }

    [Header("Star colours")]
    [Tooltip("1 - fastest.")]
    public Color bestColor = new(1f, 0.84f, 0.2f);

    [Tooltip("2 - middle.")]
    public Color goodColor = new(0.78f, 0.80f, 0.82f);

    [Tooltip("3 - slowest. Everything past the middle threshold.")]
    public Color slowColor = new(0.72f, 0.45f, 0.20f);

    [Header("Times, in order L1 to L4")]
    public Thresholds[] puzzles =
    {
        new() { label = "L1 - Climb" },
        new() { label = "L2 - Astrolabe" },
        new() { label = "L3 - Cannon" },
        new() { label = "L4 - Spices" },
    };

    public Color ColorFor(int puzzle, float seconds)
    {
        Thresholds thresholds = For(puzzle);

        if (thresholds == null)
            return slowColor;

        if (seconds <= thresholds.bestUnder) return bestColor;
        if (seconds <= thresholds.goodUnder) return goodColor;

        return slowColor;
    }

    private Thresholds For(int puzzle)
    {
        int index = puzzle - 1;

        return puzzles != null && index >= 0 && index < puzzles.Length
            ? puzzles[index]
            : null;
    }
}
