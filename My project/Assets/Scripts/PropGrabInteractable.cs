using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// An XRGrabInteractable that always falls when let go.
//
// The stock one remembers whether the Rigidbody was kinematic when grabbed and
// puts that back on release - so a prop that starts kinematic, to sit still on
// the shelf, would hang in mid-air after being dropped. Overriding the restore
// itself, rather than reacting to the release event, matters: XRI defers the
// real detach to a later update, and anything set in selectExited is quietly
// overwritten afterwards.
public class PropGrabInteractable : XRGrabInteractable
{
    protected override void SetupRigidbodyDrop(Rigidbody rigidbody)
    {
        base.SetupRigidbodyDrop(rigidbody);

        rigidbody.isKinematic = false;
        rigidbody.useGravity = true;
    }
}
