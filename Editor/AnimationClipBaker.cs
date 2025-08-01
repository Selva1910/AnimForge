using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

public static class AnimationClipBaker
{
    public static AnimationClip BakeClipWithSpeedCurve(AnimationClip sourceClip, AnimationCurve speedCurve, float frameRate = 30f)
    {
        if (sourceClip == null || speedCurve == null)
        {
            Debug.LogError("Missing source clip or speed curve");
            return null;
        }

        float duration = sourceClip.length;
        float deltaTime = 1f / frameRate;

        var bindings = AnimationUtility.GetCurveBindings(sourceClip);
        Dictionary<EditorCurveBinding, AnimationCurve> bakedCurves = new Dictionary<EditorCurveBinding, AnimationCurve>();

        foreach (var binding in bindings)
        {
            AnimationCurve sourceCurve = AnimationUtility.GetEditorCurve(sourceClip, binding);
            AnimationCurve bakedCurve = new AnimationCurve();

            float t = 0f;
            float remappedTime = 0f;

            while (t <= duration)
            {
                float speed = Mathf.Max(speedCurve.Evaluate(t), 0.0001f); // avoid division by zero
                remappedTime += deltaTime * speed;

                float value = sourceCurve.Evaluate(remappedTime);
                bakedCurve.AddKey(t, value);

                t += deltaTime;
            }

            bakedCurves[binding] = bakedCurve;
        }

        AnimationClip newClip = new AnimationClip();
        newClip.frameRate = frameRate;

        foreach (var pair in bakedCurves)
        {
            newClip.SetCurve(pair.Key.path, pair.Key.type, pair.Key.propertyName, pair.Value);
        }

        newClip.EnsureQuaternionContinuity();

        return newClip;
    }

    public static void SaveClipAsAsset(AnimationClip clip, string name = "BakedClip")
    {
        if (clip == null) return;

        string path = EditorUtility.SaveFilePanelInProject("Save Baked Animation Clip", name, "anim", "Save the baked animation");
        if (!string.IsNullOrEmpty(path))
        {
            AssetDatabase.CreateAsset(clip, path);
            AssetDatabase.SaveAssets();
            Debug.Log("Saved new baked animation at: " + path);
        }
    }

    public static AnimationClip TrimClip(AnimationClip sourceClip, float startTime, float endTime)
    {
        if (sourceClip == null || startTime >= endTime || endTime > sourceClip.length)
        {
            Debug.LogError("Invalid trim times or source clip.");
            return null;
        }

        var trimmedClip = new AnimationClip
        {
            frameRate = sourceClip.frameRate
        };

        var bindings = AnimationUtility.GetCurveBindings(sourceClip);

        foreach (var binding in bindings)
        {
            var curve = AnimationUtility.GetEditorCurve(sourceClip, binding);
            var trimmedCurve = new AnimationCurve();

            foreach (var key in curve.keys)
            {
                if (key.time >= startTime && key.time <= endTime)
                {
                    var newKey = new Keyframe(key.time - startTime, key.value, key.inTangent, key.outTangent);
                    trimmedCurve.AddKey(newKey);
                }
            }

            if (trimmedCurve.length > 0)
            {
                trimmedClip.SetCurve(binding.path, binding.type, binding.propertyName, trimmedCurve);
            }
        }

        trimmedClip.EnsureQuaternionContinuity();
        return trimmedClip;
    }


}
