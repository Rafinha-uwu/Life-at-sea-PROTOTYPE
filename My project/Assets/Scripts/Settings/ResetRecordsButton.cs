using UnityEngine;
using UnityEngine.Events;

// Wipes every best time and star, back to a fresh game. Hook Press() to a
// UI Button's On Click.
//
// Needs several presses, because a ray misfires easily in VR and this cannot
// be undone. Closing the menu starts the count again from zero.
//
// The stars and times on screen update by themselves - PuzzleRecords.Clear
// raises Changed, which every PuzzleStar listens to, and any star that is
// switched off at the time refreshes when it next appears.
public class ResetRecordsButton : MonoBehaviour
{
    [Tooltip("Presses needed to actually clear. Closing the menu resets the count.")]
    public int pressesNeeded = 5;

    [Tooltip("Optional - a sound or a flash when the records are actually wiped.")]
    public UnityEvent onCleared;

    private int presses;

    public void Press()
    {
        presses++;

        if (presses < pressesNeeded) return;

        presses = 0;

        PuzzleRecords.Clear();
        onCleared.Invoke();
    }

    // Closing settings - or the whole menu - starts the count again
    private void OnDisable()
    {
        presses = 0;
    }
}
