using UnityEngine;
using UnityEngine.UI;

// Dims the pause menu's "leave puzzle" button when there is no puzzle to leave,
// so it reads as unavailable instead of looking broken when nothing happens.
//
// Refreshing on enable is enough: the menu reopens on its main page every time,
// and no puzzle can start while the menu is up.
[RequireComponent(typeof(CanvasGroup))]
public class LeavePuzzleButton : MonoBehaviour
{
    public GAME game;

    [Tooltip("Also switched off while dimmed, so it cannot be pressed.")]
    public Button button;

    [Range(0f, 1f)] public float dimmedAlpha = 0.5f;

    private CanvasGroup group;

    private void Awake()
    {
        group = GetComponent<CanvasGroup>();
    }

    private void OnEnable()
    {
        Refresh();
    }

    public void Refresh()
    {
        bool available = game != null && game.InPuzzle;

        group.alpha = available ? 1f : dimmedAlpha;
        group.interactable = available;

        if (button != null)
            button.interactable = available;
    }
}
