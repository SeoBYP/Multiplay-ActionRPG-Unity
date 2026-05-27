using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

[InitializeOnLoad]
public static class SceneToolbarButton
{
    private const string ToolbarRootName = "ToolbarZoneRightAlign";
    private const string ButtonNamePrefix = "scene-toolbar-button-";

    private static readonly Type ToolbarType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.Toolbar");
    private static readonly SceneShortcut[] SceneShortcuts =
    {
        new("Title", "Assets/Scenes/Title.unity"),
        new("Main", "Assets/Scenes/Main.unity"),
        new("Dungeon", "Assets/Scenes/Dungeon.unity"),
    };

    private static ScriptableObject currentToolbar;

    static SceneToolbarButton()
    {
        EditorApplication.update += TryAttachToToolbar;
    }

    private static void TryAttachToToolbar()
    {
        if (currentToolbar == null)
        {
            UnityEngine.Object[] toolbars = Resources.FindObjectsOfTypeAll(ToolbarType);
            if (toolbars.Length == 0)
            {
                return;
            }

            currentToolbar = toolbars[0] as ScriptableObject;
        }

        if (currentToolbar == null)
        {
            return;
        }

        FieldInfo rootField = ToolbarType.GetField("m_Root", BindingFlags.NonPublic | BindingFlags.Instance);
        if (rootField == null)
        {
            EditorApplication.update -= TryAttachToToolbar;
            Debug.LogWarning("SceneToolbarButton: Failed to access Unity toolbar root.");
            return;
        }

        VisualElement root = rootField.GetValue(currentToolbar) as VisualElement;
        VisualElement rightZone = root?.Q(ToolbarRootName);
        if (rightZone == null)
        {
            return;
        }

        if (HasRegisteredButtons(rightZone))
        {
            EditorApplication.update -= TryAttachToToolbar;
            return;
        }

        foreach (SceneShortcut shortcut in SceneShortcuts)
        {
            rightZone.Add(CreateSceneButton(shortcut));
        }

        EditorApplication.update -= TryAttachToToolbar;
    }

    private static Button CreateSceneButton(SceneShortcut shortcut)
    {
        Button button = new(() => OpenScene(shortcut.Path))
        {
            name = $"{ButtonNamePrefix}{shortcut.Label}",
            text = shortcut.Label,
            tooltip = $"Open {shortcut.Path}"
        };

        button.style.marginLeft = 6;
        button.style.minWidth = 80;
        button.SetEnabled(AssetDatabase.LoadAssetAtPath<SceneAsset>(shortcut.Path) != null);
        return button;
    }

    private static bool HasRegisteredButtons(VisualElement rightZone)
    {
        foreach (SceneShortcut shortcut in SceneShortcuts)
        {
            if (rightZone.Q<Button>($"{ButtonNamePrefix}{shortcut.Label}") != null)
            {
                return true;
            }
        }

        return false;
    }

    private static void OpenScene(string scenePath)
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        EditorSceneManager.OpenScene(scenePath);
    }

    private readonly struct SceneShortcut
    {
        public SceneShortcut(string label, string path)
        {
            Label = label;
            Path = path;
        }

        public string Label { get; }
        public string Path { get; }
    }
}
