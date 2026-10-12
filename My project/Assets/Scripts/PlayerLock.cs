using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Gravity;

// Holds the player still on the rowboat without freezing the world - the ship
// has to keep rocking in the background - so timeScale stays at 1 and only the
// player's own locomotion is switched off.
//
// Everything in XRI that moves the rig derives from LocomotionProvider (move,
// turn, teleport, climb, grab-move), so one sweep catches the lot whatever the
// rig ends up carrying.
public class PlayerLock : MonoBehaviour
{
    [Tooltip("XR Origin root. Leave empty to use this object.")]
    public GameObject player;

    [Tooltip("Cannot be opened while locked. The start menu is the only menu out on the rowboat.")]
    public PauseMenu pauseMenu;

    [Tooltip("Leave off so the player still settles onto the rowboat floor.")]
    public bool lockGravity = false;

    [Tooltip("Anything else to switch off while locked.")]
    public Behaviour[] alsoDisable;

    private readonly List<Behaviour> disabled = new();

    public bool IsLocked { get; private set; }

    public void Lock()
    {
        if (IsLocked) return;
        IsLocked = true;

        GameObject root = player != null ? player : gameObject;

        foreach (LocomotionProvider provider in
                 root.GetComponentsInChildren<LocomotionProvider>(true))
        {
            if (!lockGravity && provider is GravityProvider) continue;
            Disable(provider);
        }

        if (alsoDisable != null)
        {
            foreach (Behaviour behaviour in alsoDisable)
                Disable(behaviour);
        }

        if (pauseMenu != null) pauseMenu.SetLocked(true);
    }

    public void Unlock()
    {
        if (!IsLocked) return;
        IsLocked = false;

        // Only switch back on what this actually switched off, so a provider
        // that was already disabled for its own reasons stays that way.
        foreach (Behaviour behaviour in disabled)
        {
            if (behaviour != null) behaviour.enabled = true;
        }

        disabled.Clear();

        if (pauseMenu != null) pauseMenu.SetLocked(false);
    }

    private void Disable(Behaviour behaviour)
    {
        if (behaviour == null || !behaviour.enabled) return;

        behaviour.enabled = false;
        disabled.Add(behaviour);
    }
}
