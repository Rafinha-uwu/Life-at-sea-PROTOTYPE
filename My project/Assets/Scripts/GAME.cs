using System.Collections;
using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class GAME : MonoBehaviour
{
    // Start  - on the rowboat, start menu up.
    // Explore - aboard, free to roam, puzzles started from the ship's buttons.
    // Puzzle  - sent straight to one puzzle from the menu; finishing it
    //           returns to the rowboat rather than to the hub.
    private enum Mode { Start, Explore, Puzzle }

    [Header("Core")]
    public GameObject Fade;
    public GameObject Player;

    [Tooltip("Also turn the player to face the destination's blue Z axis. Off by default - only useful if the entry points are actually aimed.")]
    public bool alignFacingOnTeleport = false;

    [Tooltip("Frames to keep re-applying a teleport while the headset pose settles. 0 disables it.")]
    public int settleFrames = 10;

    [Header("Start - rowboat")]
    [Tooltip("Rowboat and start menu together. Hidden while the player is aboard the ship.")]
    public GameObject StartArea;

    [Tooltip("Where the player stands on the rowboat.")]
    public Transform TargetStart;

    [Tooltip("The rowboat transform. The player is parented to it so the ship's roll cannot swing them around out here.")]
    public Transform RowBoat;

    [Tooltip("Start menu pages: 0 main, 1 settings, 2 puzzles.")]
    public MenuPages StartMenuPages;

    [Tooltip("Page to reopen on after finishing a puzzle picked from the menu.")]
    public int puzzlePage = 2;

    public OceanHeight Ocean;

    [Tooltip("Freezes the player's walking and blocks the pause menu while the start menu is up.")]
    public PlayerLock Locomotion;

    [Header("Hub")]
    public GameObject HUB;
    public GameObject TargetHUB;

    [Header("L1 - Climb")]
    public GameObject Level1;
    public GameObject Target1;
    public GameObject P1;
    public GameObject M1;

    [Tooltip("Where the Puzzles menu drops the player for this level.")]
    public Transform Entry1;

    [Tooltip("Bottom of the ladder. Only L1 returns the player here.")]
    public Transform L1ReturnPoint;

    [Header("L2 - Astrolabe")]
    public GameObject Level2;
    public GameObject Target2;
    public GameObject P2;
    public GameObject M2;
    public Transform Entry2;

    [Tooltip("Sky goes to night for this puzzle. Driven here rather than from the ship's button, so it happens however the level was started.")]
    public SunXRotator Sky;

    [Header("L3 - Cannon")]
    public GameObject Level3;
    public GameObject Targets;
    public GameObject Target3;
    public GameObject P3;
    public GameObject M3;
    public Transform Entry3;

    [Tooltip("Hidden while the cannon level runs (the dancing sailors), shown again when it ends. One parent object.")]
    public GameObject HideDuringL3;

    [Tooltip("Sun X rotation for the cannon level. Set instantly while the screen is black, and back to day the same way. Uses the Sky field under L2.")]
    public float L3SunX = -2f;

    [Header("L4 - Spices")]
    public GameObject Level4;
    public GameObject Target4;
    public GameObject P4;
    public GameObject M4;
    public GameObject Table;
    public Transform Entry4;

    [Header("Win and Music")]
    [Tooltip("Pops up on a win with the time, star and new-record label. Hidden again once the screen is black.")]
    public VictoryPanel Victory;

    [Tooltip("Switches to each level's track while the screen is black, and back to the main theme after.")]
    public MusicDirector Music;

    [Header("Timing")]
    [Tooltip("Seconds to go fully black. The In/Out clips are sped up to match, so this one number is the whole transition.")]
    public float fadeTime = 2f;

    [Tooltip("Authored length of the In and Out clips. Leave at 4 unless you re-author them.")]
    public float fadeClipLength = 4f;

    public float fadeHoldTime = 1f;
    public float winPauseTime = 5f;
    public float spiceIntroDelay = 20f;

    [Header("Cannon Targets")]
    public int targetsNeeded = 3;

    [HideInInspector] public int targetsDestroyed;

    private Animator fadeAnimator;
    private Fall playerFall;
    private VRCollectMinigame spiceGame;
    private CannonLoader cannon;
    private NestRandomActivator nest;
    private XRInteractionManager interactionManager;
    private Coroutine settling;
    private XROrigin xrOrigin;
    private bool transitioning;
    private int currentLevel;
    private Mode mode = Mode.Start;

    // Scaled time, so a paused game does not count against the record.
    // Negative means no run is being timed.
    private float levelStartTime = -1f;

    // The ship root the player normally rides, remembered before the rowboat
    // takes them off it.
    private Transform playerHome;

    private void Awake()
    {
        fadeAnimator = Fade.GetComponent<Animator>();

        // The clips are authored at 4s; play them faster rather than making
        // the code wait a different length than the animation takes.
        if (fadeAnimator != null && fadeTime > 0f && fadeClipLength > 0f)
            fadeAnimator.speed = fadeClipLength / fadeTime;

        // A fade must always finish. On scaled time, anything that leaves
        // timeScale at 0 - a pause menu opened mid-transition - freezes the
        // clip and the waits together and strands the player on black.
        if (fadeAnimator != null)
            fadeAnimator.updateMode = AnimatorUpdateMode.UnscaledTime;

        playerFall = Player.GetComponent<Fall>();
        spiceGame = Table != null ? Table.GetComponent<VRCollectMinigame>() : null;
        cannon = FindAnyObjectByType<CannonLoader>(FindObjectsInactive.Include);
        nest = FindAnyObjectByType<NestRandomActivator>(FindObjectsInactive.Include);

        // Falls only matter on the mast
        if (playerFall != null) playerFall.enabled = false;

        xrOrigin = Player.GetComponentInChildren<XROrigin>(true);
        playerHome = Player.transform.parent;

        OpenStartArea(0);
    }

    public void TargetDestroyed()
    {
        targetsDestroyed++;

        if (targetsDestroyed >= targetsNeeded)
            Hub();
    }

    // Ship buttons - the player is already standing at the puzzle
    public void L1() => StartLevel(1);
    public void L2() => StartLevel(2);
    public void L3() => StartLevel(3);
    public void L4() => StartLevel(4);

    // Start menu buttons. The ContextMenu entries are an editor escape hatch:
    // right-click the GAME component in play mode to move without the UI, for
    // when something is wrong with pointing and the menu cannot be pressed.
    [ContextMenu("Go: Explore")]
    public void Explore()
    {
        if (Busy("Explore")) return;
        StartCoroutine(GoExplore());
    }

    // Every menu button is gated on `transitioning`, so if it ever sticks on,
    // the whole menu highlights normally and does nothing at all. Say so out
    // loud rather than letting it look like broken input.
    private bool Busy(string what)
    {
        if (!transitioning) return false;

        Debug.LogWarning(
            $"GAME: '{what}' ignored because a transition is still running. " +
            "If this does not clear, a fade coroutine died partway.", this);

        return true;
    }

    [ContextMenu("Go: Puzzle 1")]
    public void Puzzle1() => StartFromMenu(1);
    public void Puzzle2() => StartFromMenu(2);
    public void Puzzle3() => StartFromMenu(3);
    public void Puzzle4() => StartFromMenu(4);

    public void Quit()
    {
        Time.timeScale = 1f;

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // Reaching the hub this way means the puzzle was actually finished
    public void Hub()
    {
        if (Busy("Hub")) return;

        // Before the victory pause, or the record gains those seconds
        SubmitTime();

        StartCoroutine(GoHub(winPauseTime, mode == Mode.Puzzle));
    }

    // Is there a puzzle running right now? The pause menu dims its
    // "leave puzzle" button when there is not.
    public bool InPuzzle => currentLevel != 0;

    // Pause-menu "exit": back out to the rowboat from wherever the player is,
    // puzzle or not. Not the same as Quit, which closes the game.
    [ContextMenu("Go: Main Menu")]
    public void ToMainMenu()
    {
        if (Busy("Main menu")) return;

        // Backing out is not a finish - throw any run away
        levelStartTime = -1f;

        Time.timeScale = 1f;
        StartCoroutine(GoHub(0f, true));
    }

    // Menu "leave puzzle" - same fade, no victory pause
    public void LeaveLevel()
    {
        if (Busy("Leave puzzle") || currentLevel == 0) return;

        // Walking out is not a finish - throw the run away
        levelStartTime = -1f;

        Time.timeScale = 1f;
        StartCoroutine(GoHub(0f, mode == Mode.Puzzle));
    }

    // Only reached by actually finishing - so this is also where the victory
    // card goes up. Leaving a level never gets here, so it never shows then.
    private void SubmitTime()
    {
        if (currentLevel >= 1 && levelStartTime >= 0f)
        {
            float seconds = Time.time - levelStartTime;

            // Asked before submitting, or the new time would count as "previous"
            bool hadPrevious = PuzzleRecords.Has(currentLevel);
            bool improved = PuzzleRecords.Submit(currentLevel, seconds);

            if (Victory != null)
                Victory.Show(currentLevel, seconds, improved, hadPrevious);
        }

        levelStartTime = -1f;
    }

    private void StartLevel(int index)
    {
        if (Busy($"L{index}")) return;
        StartCoroutine(RunLevel(index, false));
    }

    private void StartFromMenu(int index)
    {
        if (Busy($"Puzzle {index}")) return;

        mode = Mode.Puzzle;
        StartCoroutine(RunLevel(index, true));
    }

    private IEnumerator GoExplore()
    {
        yield return FadeIn();

        mode = Mode.Explore;
        BoardShip(TargetHUB.transform);
        ShowLevelButtons(true);

        yield return FadeOut();
    }

    private IEnumerator RunLevel(int index, bool fromStartMenu)
    {
        currentLevel = index;

        yield return FadeIn();

        // Straight to the puzzle, already running - no ship button to press
        if (fromStartMenu)
            BoardShip(EntryFor(index));

        // Hide hub entries
        Target1.SetActive(false);
        Target2.SetActive(false);
        Target3.SetActive(false);
        Target4.SetActive(false);

        // Change tracks while the screen is black
        if (Music != null) Music.PlayLevel(index);

        switch (index)
        {
            case 1:
                Level1.SetActive(true);
                RestoreHandles();
                P1.SetActive(true);
                break;
            case 2:
                Level2.SetActive(true);
                P2.SetActive(true);
                if (Sky != null) Sky.TransitionToNight();
                break;
            case 3:
                targetsDestroyed = 0;
                Level3.SetActive(true);
                Targets.SetActive(true);
                RestoreTargets();
                if (cannon != null) cannon.ResetForLevel();
                if (HideDuringL3 != null) HideDuringL3.SetActive(false);
                if (Sky != null) Sky.SnapTo(L3SunX);
                P3.SetActive(true);
                break;
            case 4:
                Level4.SetActive(true);
                P4.SetActive(true);
                break;
        }

        yield return FadeOut();

        switch (index)
        {
            case 1: Speak(M1, 1); break;
            case 2: Speak(M2, 2); break;
            case 3: Speak(M3, 3); break;
            case 4:
                Speak(M4, 4);
                yield return WaitForBriefing(M4);

                // The orders go on the table's own board from here, so put the
                // captain's bubble away rather than have two places to read
                Silence(M4, P4);

                if (spiceGame != null) spiceGame.StartGame();
                break;
        }

        // Clock starts when the player can actually act - after the fade, and
        // after L4's briefing, so the captain's talking is not charged to them.
        levelStartTime = Time.time;
    }

    private IEnumerator GoHub(float pause, bool backToRowboat)
    {
        if (pause > 0f) yield return new WaitForSecondsRealtime(pause);
        yield return FadeIn();

        // Whatever happens in here, the screen has to come back. An exception
        // escaping a coroutine kills it where it stands, and in this one that
        // means the player is left staring at a black quad forever.
        try
        {
            TearDown(backToRowboat);
        }
        catch (System.Exception error)
        {
            Debug.LogError(
                $"GAME teardown failed (level {currentLevel}, back to rowboat: {backToRowboat}). " +
                "The fade still completes, but the player was probably not moved.", this);
            Debug.LogException(error, this);
        }

        currentLevel = 0;

        yield return FadeOut();
    }

    private void TearDown(bool backToRowboat)
    {
        // Let go of anything held BEFORE the level containers switch off.
        // Deactivating a grabbed object leaves the interactor still holding it:
        // the quadrant rode along in hand, and on replay its selectEntered
        // never fired again, so it sat there never detecting the star.
        ReleaseHands();

        // The screen is black by now - put the card away unseen, ready for
        // the next win, and hand the music back to the main theme
        if (Victory != null) Victory.Hide();
        if (Music != null) Music.PlayMain();
        if (HideDuringL3 != null) HideDuringL3.SetActive(true);

        if (spiceGame != null) spiceGame.StopGame();

        // Sweep up anything grabbed from the piles
        SpawnedItem.ClearAll();

        // Stop the trajectory line and clear the bird/island targets
        if (cannon != null) cannon.ResetCannon();
        if (nest != null) nest.HideAll();

        Level1.SetActive(false);
        Level2.SetActive(false);
        Level3.SetActive(false);
        Level4.SetActive(false);
        Targets.SetActive(false);

        // Silence tutorial sailors
        Silence(M1, P1);
        Silence(M2, P2);
        Silence(M3, P3);
        Silence(M4, P4);

        Target1.SetActive(true);
        Target2.SetActive(true);
        Target3.SetActive(true);
        Target4.SetActive(true);

        // Only L2 takes the sky to night, so only L2 gives it back - leaving
        // any day/night the player chose while exploring alone.
        if (currentLevel == 2 && Sky != null)
            Sky.TransitionToDay();

        // L3's sun comes back instantly - this runs while the screen is black
        if (currentLevel == 3 && Sky != null)
            Sky.SnapToDay();

        if (backToRowboat)
        {
            // Coming out of a puzzle picked from the menu, reopen on the puzzle
            // list. Backing out of exploring, the main page is where they were.
            // `mode` still holds the old one here - OpenStartArea resets it.
            OpenStartArea(mode == Mode.Puzzle ? puzzlePage : 0);
        }
        else if (currentLevel == 1 && L1ReturnPoint != null)
        {
            // Only the mast strands the player
            MovePlayerTo(L1ReturnPoint);
        }
    }

    private void OpenStartArea(int page)
    {
        mode = Mode.Start;

        // The ship's level buttons are for exploring only - out here they
        // would just float in the distance
        ShowLevelButtons(false);

        // Show it before the player is parented to it, or activating the
        // rowboat later would be the only thing bringing them back.
        if (StartArea != null) StartArea.SetActive(true);
        if (Ocean != null) Ocean.RaiseForStartMenu();
        if (StartMenuPages != null) StartMenuPages.ShowPage(page);

        PlacePlayer(TargetStart, RowBoat);

        // Anyone shoved overboard last time is back on deck for the next visit,
        // and anything picked up is back where it was found
        SailorBump.ResetAll();
        GrabbableProp.ResetAll();

        // Stand still and watch the ship - the world keeps moving, the player does not
        if (Locomotion != null) Locomotion.Lock();
    }

    private void ShowLevelButtons(bool visible)
    {
        if (Target1 != null) Target1.SetActive(visible);
        if (Target2 != null) Target2.SetActive(visible);
        if (Target3 != null) Target3.SetActive(visible);
        if (Target4 != null) Target4.SetActive(visible);
    }

    private void BoardShip(Transform destination)
    {
        // Player first: they are parented to the rowboat, so hiding the start
        // area while they are still on it would hide the player too.
        PlacePlayer(destination, playerHome);

        if (StartArea != null) StartArea.SetActive(false);
        if (Ocean != null) Ocean.LowerForShip();
        if (Locomotion != null) Locomotion.Unlock();
    }

    private Transform EntryFor(int index)
    {
        Transform entry = index switch
        {
            1 => Entry1,
            2 => Entry2,
            3 => Entry3,
            4 => Entry4,
            _ => null,
        };

        // Better to land at the hub than to stay out on the rowboat
        return entry != null ? entry : TargetHUB.transform;
    }

    private IEnumerator FadeIn()
    {
        transitioning = true;

        if (playerFall != null)
            playerFall.enabled = false;

        fadeAnimator.Play("In");
        yield return new WaitForSecondsRealtime(fadeTime);
    }

    private IEnumerator FadeOut()
    {
        yield return new WaitForSecondsRealtime(fadeHoldTime);

        fadeAnimator.Play("Out");

        if (playerFall != null)
        {
            playerFall.ResetFallTracking();
            playerFall.enabled = currentLevel == 1;
        }

        transitioning = false;
    }

    private void MovePlayerTo(Transform destination)
    {
        PlacePlayer(destination, Player.transform.parent);
    }

    // Cancels every live grab on the player. Only called when a level ends -
    // NOT on every teleport, which is what made the player drop a carried
    // spice just for being moved.
    private void ReleaseHands()
    {
        if (interactionManager == null)
            interactionManager = FindAnyObjectByType<XRInteractionManager>();

        if (interactionManager == null) return;

        foreach (XRBaseInteractor interactor in
                 Player.GetComponentsInChildren<XRBaseInteractor>(true))
        {
            // Everything goes - the level is ending, and that is intended
            // even for a prop carried in from the deck. Cast, or the
            // deprecated XRBaseInteractor overload wins
            if (interactor.hasSelection)
                interactionManager.CancelInteractorSelection((IXRSelectInteractor)interactor);
        }
    }


    private void PlacePlayer(Transform destination, Transform parent)
    {
        CharacterController controller = Player.GetComponent<CharacterController>();

        // Controller ignores raw writes
        if (controller != null) controller.enabled = false;

        if (Player.transform.parent != parent)
            Player.transform.SetParent(parent, true);

        if (destination != null)
            MoveRigTo(destination);

        if (controller != null) controller.enabled = true;

        // The headset reports no pose for the first frames after load, so the
        // drift correction in MoveRigTo has nothing to correct against yet and
        // computes zero - which is exactly the "I spawn far from the rowboat"
        // bug on a cold start. Re-apply it for a few frames once tracking has
        // caught up. It all happens behind the fade, so nothing is visible.
        if (destination != null && isActiveAndEnabled)
        {
            if (settling != null) StopCoroutine(settling);
            settling = StartCoroutine(SettleRig(destination));
        }
    }

    private IEnumerator SettleRig(Transform destination)
    {
        for (int frame = 0; frame < settleFrames; frame++)
        {
            yield return null;

            if (destination == null) break;

            CharacterController controller = Player.GetComponent<CharacterController>();

            if (controller != null) controller.enabled = false;
            MoveRigTo(destination);
            if (controller != null) controller.enabled = true;
        }

        settling = null;
    }

    // The headset sits wherever the player's real-world tracking space left it,
    // which is usually not over the rig origin. Moving the rig therefore puts
    // the *origin* on the target and leaves the player standing off to one
    // side - metres away, if they set the headset down before starting.
    // Recentring by hand hides it; this fixes it properly by offsetting the rig
    // so the player's head lands on the target instead.
    private void MoveRigTo(Transform destination)
    {
        Transform rig = Player.transform;

        // Rotate first - it moves the head, which the offset below depends on
        if (alignFacingOnTeleport && xrOrigin != null)
            xrOrigin.MatchOriginUpCameraForward(destination.up, destination.forward);

        Camera head = xrOrigin != null ? xrOrigin.Camera : null;

        if (head == null)
        {
            rig.position = destination.position;
            return;
        }

        // Horizontal only: the rig origin is the floor, and the head is not
        Vector3 drift = head.transform.position - rig.position;
        drift.y = 0f;

        rig.position = destination.position - drift;
    }

    // Restore shot targets
    private void RestoreTargets()
    {
        foreach (Transform target in Targets.transform)
            target.gameObject.SetActive(true);
    }

    // Regrow rungs broken last run
    private void RestoreHandles()
    {
        foreach (BreakableHandle handle in
                 Level1.GetComponentsInChildren<BreakableHandle>(true))
        {
            handle.Restore();
        }
    }

    // Briefing end or timeout
    private IEnumerator WaitForBriefing(GameObject sailor)
    {
        Talk talk = sailor != null ? sailor.GetComponent<Talk>() : null;
        float elapsed = 0f;

        while (talk != null && talk.IsTalking && elapsed < spiceIntroDelay)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private void Speak(GameObject sailor, int lineSet)
    {
        if (sailor == null) return;

        Talk talk = sailor.GetComponent<Talk>();
        if (talk != null) talk.Say(lineSet);
    }

    private void Silence(GameObject sailor, GameObject bubble)
    {
        if (sailor != null)
        {
            Talk talk = sailor.GetComponent<Talk>();
            if (talk != null) talk.StopTalking();
        }

        if (bubble != null) bubble.SetActive(false);
    }
}
