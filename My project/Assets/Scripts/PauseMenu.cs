using UnityEngine;
using UnityEngine.InputSystem;

public class PauseMenu : MonoBehaviour
{
    [Header("Menu")]
    [Tooltip("The panel to show/hide. Must NOT be this object.")]
    public GameObject menu;
    public bool startHidden = true;

    [Header("Input")]
    [Tooltip("Leave empty to use the built-in menu button bindings.")]
    public InputActionProperty toggleInput;

    [Header("Pause")]
    public bool pauseTimeScale = true;

    [Header("Leave Puzzle")]
    [Tooltip("Needed for the Leave Puzzle button.")]
    public GAME game;

    private InputAction fallbackAction;
    private VRMenuFollower follower;
    private MenuPages pages;
    private bool locked;

    // Pages win if present, so Options can live beside the main panel
    public bool IsOpen => pages != null ? pages.AnyPageOpen
                                        : menu != null && menu.activeSelf;

    private void Awake()
    {
        follower = GetComponent<VRMenuFollower>();
        pages = GetComponent<MenuPages>();

        fallbackAction = new InputAction("ToggleMenu", InputActionType.Button);
        fallbackAction.AddBinding("<XRController>{LeftHand}/menuButton");
        fallbackAction.AddBinding("<XRController>{LeftHand}/menu");
        fallbackAction.AddBinding("<XRController>{LeftHand}/start");
        fallbackAction.AddBinding("<OculusTouchController>{LeftHand}/start");
        fallbackAction.AddBinding("<MetaQuestTouchProController>{LeftHand}/menu");
        fallbackAction.AddBinding("<Keyboard>/escape");

        if (startHidden)
            SetMenuOpen(false);
    }

    private void OnEnable()
    {
        InputAction action = ActiveAction;
        if (action == null) return;

        action.performed += OnTogglePerformed;
        action.Enable();
    }

    private void OnDisable()
    {
        InputAction action = ActiveAction;
        if (action == null) return;

        action.performed -= OnTogglePerformed;

        if (action == fallbackAction)
            action.Disable();
    }

    private void OnDestroy()
    {
        fallbackAction?.Dispose();
    }

    // An unassigned property returns an empty action, not null
    private InputAction ActiveAction
    {
        get
        {
            InputAction assigned = toggleInput.action;

            if (assigned != null && assigned.bindings.Count > 0)
                return assigned;

            return fallbackAction;
        }
    }

    private void OnTogglePerformed(InputAction.CallbackContext context)
    {
        ToggleMenu();
    }

    // Blocked while the player is out on the rowboat - see PlayerLock
    public void SetLocked(bool value)
    {
        locked = value;

        if (locked && IsOpen)
            SetMenuOpen(false);
    }

    public void ToggleMenu()
    {
        if (locked) return;

        SetMenuOpen(!IsOpen);
    }

    public void PauseGame()
    {
        if (locked) return;

        SetMenuOpen(true);
    }

    public void ResumeGame()
    {
        SetMenuOpen(false);
    }

    // Close the menu first, or the fade coroutine stalls at timeScale 0
    public void LeaveCurrentPuzzle()
    {
        SetMenuOpen(false);

        if (game != null)
            game.LeaveLevel();
    }

    // "Exit" in the in-game menu: out to the rowboat, not out of the game.
    // Close first, or the fade coroutine stalls at timeScale 0.
    public void GoToMainMenu()
    {
        SetMenuOpen(false);

        if (game != null)
            game.ToMainMenu();
    }

    public void QuitGame()
    {
        Time.timeScale = 1f;

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void SetMenuOpen(bool open)
    {
        if (pages != null)
        {
            // Always reopen on the main page, never mid-Options
            if (open) pages.ShowMain();
            else pages.CloseAll();
        }
        else if (menu != null)
        {
            menu.SetActive(open);
        }

        // Follower re-snaps on enable
        if (follower != null)
            follower.enabled = open;

        if (pauseTimeScale)
            Time.timeScale = open ? 0f : 1f;
    }
}
