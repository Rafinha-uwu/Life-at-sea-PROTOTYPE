using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// The VR hands blend Idle / Grip / Pinch on a 2D tree driven by the Grip and
// Pinch floats. This swaps the pinch corner for a pointing finger - visuals
// only, the input and the parameters are untouched.
//
// The Point clip is built from the Grip clip by dropping the index-finger
// curves: LeftHandIdle.anim has no curves at all, so the model's bind pose is
// the open hand, and a finger nobody animates simply stays open while the rest
// of the hand closes. No posing by hand, and it matches the existing fist
// exactly because it *is* the existing fist.
//
// One-shot. Safe to delete this file once the clips exist and the tree is set.
public static class NauHandPose
{
    private const string Folder = "Assets/OtherAssets/VR Hands/Animations";

    [MenuItem("Nau/Hands/Use Pointing Finger")]
    private static void UsePointing() => Apply(true);

    [MenuItem("Nau/Hands/Restore Pinch")]
    private static void RestorePinch() => Apply(false);

    private static void Apply(bool pointing)
    {
        foreach (string side in new[] { "Left", "Right" })
        {
            AnimationClip pinch = Load<AnimationClip>($"{Folder}/{side}HandPinch.anim");
            AnimationClip grip = Load<AnimationClip>($"{Folder}/{side}HandGrip.anim");

            AnimatorController controller =
                Load<AnimatorController>($"{Folder}/Controllers/{side}HandController.controller");

            if (pinch == null || grip == null || controller == null)
            {
                Debug.LogError($"{side} hand: could not find the clips or the controller under {Folder}.");
                continue;
            }

            AnimationClip point = pointing ? BuildPoint(side, grip) : Load<AnimationClip>($"{Folder}/{side}HandPoint.anim");

            if (point == null)
            {
                Debug.LogWarning($"{side} hand: no Point clip to swap. Run 'Use Pointing Finger' first.");
                continue;
            }

            AnimationClip from = pointing ? pinch : point;
            AnimationClip to = pointing ? point : pinch;

            int swapped = 0;

            foreach (AnimatorControllerLayer layer in controller.layers)
                swapped += Swap(layer.stateMachine, from, to);

            EditorUtility.SetDirty(controller);
            Debug.Log($"{side} hand: {swapped} slot(s) now play {to.name}.", controller);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static AnimationClip BuildPoint(string side, AnimationClip grip)
    {
        AnimationClip built = new() { name = $"{side}HandPoint", frameRate = grip.frameRate };

        foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(grip))
        {
            // The index is the whole point - leave it unanimated and it holds
            // the open bind pose while the rest of the hand makes a fist.
            if (binding.path.ToLowerInvariant().Contains("index"))
                continue;

            AnimationUtility.SetEditorCurve(built, binding, AnimationUtility.GetEditorCurve(grip, binding));
        }

        AnimationUtility.SetAnimationClipSettings(built, AnimationUtility.GetAnimationClipSettings(grip));

        string path = $"{Folder}/{side}HandPoint.anim";
        AnimationClip existing = Load<AnimationClip>(path);

        // Overwrite in place rather than recreating: CreateAsset over an
        // existing path hands out a new GUID and breaks the blend tree's link.
        if (existing != null)
        {
            EditorUtility.CopySerialized(built, existing);
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(built);
            return existing;
        }

        AssetDatabase.CreateAsset(built, path);
        return built;
    }

    private static int Swap(AnimatorStateMachine machine, AnimationClip from, AnimationClip to)
    {
        int count = 0;

        foreach (ChildAnimatorState child in machine.states)
        {
            if (child.state.motion == from)
            {
                child.state.motion = to;
                count++;
            }
            else if (child.state.motion is BlendTree tree)
            {
                count += Swap(tree, from, to);
            }
        }

        foreach (ChildAnimatorStateMachine sub in machine.stateMachines)
            count += Swap(sub.stateMachine, from, to);

        return count;
    }

    private static int Swap(BlendTree tree, AnimationClip from, AnimationClip to)
    {
        ChildMotion[] children = tree.children;
        int count = 0;

        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].motion == from)
            {
                children[i].motion = to;
                count++;
            }
            else if (children[i].motion is BlendTree nested)
            {
                count += Swap(nested, from, to);
            }
        }

        // Assigning the array back is what actually persists the change
        if (count > 0) tree.children = children;

        return count;
    }

    private static T Load<T>(string path) where T : Object
    {
        return AssetDatabase.LoadAssetAtPath<T>(path);
    }
}
