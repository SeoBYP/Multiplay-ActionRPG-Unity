using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Object = UnityEngine.Object;

[System.Serializable]
public class AnimationEventCopierWindow : EditorWindow
{
    private Object _source;
    private string _animationEventLog = "";
    private Vector2 _scrollPosition;
    private int _frameRate = 30;

    [MenuItem("Window/Infinity PBR/Animation/Animation Event Copier")]
    public static void ShowWindow()
    {
        GetWindow<AnimationEventCopierWindow>("Animation Event Copier");
    }

    private void OnGUI()
    {
        DrawFrameRateField();
        DrawSourceField();
        DrawLogAnimationEventsButton();
        DrawAnimationEventLog();
    }
    
    private void DrawFrameRateField()
    {
        _frameRate = EditorGUILayout.IntField("Frame Rate", _frameRate);
    }

    private void DrawSourceField()
    {
        GUILayout.Label("Source Model or Source Folder", EditorStyles.boldLabel);
        _source = EditorGUILayout.ObjectField("Source", _source, typeof(Object), false);
    }

    private void DrawLogAnimationEventsButton()
    {
        if (GUILayout.Button("Log Animation Events"))
            LogAnimationEvents();
    }

    private void DrawAnimationEventLog()
    {
        EditorGUILayout.LabelField("Animation Event Log:");
        _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, GUILayout.Height(position.height - 100));
        EditorGUILayout.TextArea(_animationEventLog, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
    }

    private void LogAnimationEvents()
    {
        _animationEventLog = "";
        StringBuilder builder = new StringBuilder();
        string path = AssetDatabase.GetAssetPath(_source);

        if (File.GetAttributes(path).HasFlag(FileAttributes.Directory))
        {
            Debug.Log($"Checking directory: {path}");
            string[] files = Directory.GetFiles(path, "*.FBX", SearchOption.AllDirectories);

            Debug.Log($"Found {files.Length} FBX files.");

            foreach (string file in files)
            {
                Debug.Log($"Checking file: {file}");
                Object[] objs = AssetDatabase.LoadAllAssetsAtPath(file);

                foreach (Object obj in objs)
                {
                    if (obj is AnimationClip)
                    {
                        AnimationClip sourceClip = (AnimationClip)obj;
                        AnimationEvent[] events = AnimationUtility.GetAnimationEvents(sourceClip);
                        if (events.Length > 0)
                        {
                            LogAnimationEventsForClip(sourceClip, builder, events);
                        }
                    }
                }
            }
        }
        else if (_source is GameObject)
        {
            Debug.Log($"source is a GameObject {_source.name}");
            GameObject model = (GameObject)_source;
            Object[] objs = AssetDatabase.LoadAllAssetsAtPath(path);

            foreach (Object obj in objs)
            {
                if (obj is AnimationClip)
                {
                    AnimationClip sourceClip = (AnimationClip)obj;
                    AnimationEvent[] events = AnimationUtility.GetAnimationEvents(sourceClip);
                    if (events.Length > 0)
                    {
                        LogAnimationEventsForClip(sourceClip, builder, events);
                    }
                }
            }
        }

        _animationEventLog = builder.ToString();
    }


    private void LogAnimationEventsForClip(AnimationClip clip, StringBuilder builder, AnimationEvent[] events)
    {
        builder.AppendLine($"ANIMATION CLIP: {clip.name}");

        foreach (var evt in events)
        {
            var frame = Mathf.RoundToInt(evt.time * _frameRate);
            var percent = Mathf.RoundToInt((evt.time / clip.length) * 100);
            builder.AppendLine($"{evt.functionName} - Frame: {frame} ({percent}% of the clip)");
            builder.AppendLine($"Int: {evt.intParameter} | Float: {evt.floatParameter} | String: {evt.stringParameter}");
            builder.AppendLine();
        }

        builder.AppendLine();
    }
}
