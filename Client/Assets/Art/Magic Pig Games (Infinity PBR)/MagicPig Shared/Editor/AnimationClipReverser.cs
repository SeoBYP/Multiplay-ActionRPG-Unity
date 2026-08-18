using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.Linq;
using System;
using System.IO;

public class AnimationClipReverser
{
    [MenuItem("Window/Infinity PBR/Animation/Duplicate and Reverse")]
    private static void DuplicateAndReverseAnimationClip()
    {
        AnimationClip originalClip = Selection.activeObject as AnimationClip;
        if (originalClip == null)
        {
            Debug.Log("No animation clip selected.");
            return;
        }

        // Duplicate
        var path = AssetDatabase.GetAssetPath(originalClip);
        var newClip = UnityEngine.Object.Instantiate(originalClip);
        newClip.name = GenerateNewClipName(originalClip.name);

        // Reverse
        foreach (var binding in AnimationUtility.GetCurveBindings(originalClip))
        {
            AnimationCurve curve = AnimationUtility.GetEditorCurve(originalClip, binding);
            Keyframe[] keyframes = curve.keys;
            for (int i = 0; i < keyframes.Length; i++)
            {
                Keyframe key = keyframes[i];
                key.time = originalClip.length - key.time;
                key.inTangent = -key.inTangent;
                key.outTangent = -key.outTangent;
                keyframes[i] = key;
            }
            Array.Reverse(keyframes);
            curve.keys = keyframes;
            newClip.SetCurve(binding.path, binding.type, binding.propertyName, curve);
        }

        // Save new clip
        var directory = Path.GetDirectoryName(path);
        var newClipPath = Path.Combine(directory, newClip.name + ".anim");
        newClipPath = AssetDatabase.GenerateUniqueAssetPath(newClipPath);
        AssetDatabase.CreateAsset(newClip, newClipPath);
        AssetDatabase.SaveAssets();
    }

    private static string GenerateNewClipName(string originalName)
    {
        if (originalName.Contains("Forward") || originalName.Contains("forward"))
        {
            return originalName.Replace("Forward", "Backward").Replace("forward", "backward");
        }
        else if (originalName.Contains("Backward") || originalName.Contains("backward"))
        {
            return originalName.Replace("Backward", "Forward").Replace("backward", "forward");
        }
        else if (originalName.Contains("Left") || originalName.Contains("left"))
        {
            return originalName.Replace("Left", "Right").Replace("left", "right");
        }
        else if (originalName.Contains("Right") || originalName.Contains("right"))
        {
            return originalName.Replace("Right", "Left").Replace("right", "left");
        }
        else
        {
            return originalName + " Reversed";
        }
    }

    // Validate the menu.
    // The item will be disabled if no AnimationClip is selected.
    [MenuItem("Window/Infinity PBR/Animation/Duplicate and Reverse", true)]
    private static bool ValidateDuplicateAndReverseAnimationClip()
    {
        return Selection.activeObject is AnimationClip;
    }
}
