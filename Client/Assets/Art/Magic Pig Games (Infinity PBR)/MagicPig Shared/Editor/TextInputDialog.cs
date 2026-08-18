using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using static MagicPigGames.Shared.MagicPigEditor;

namespace MagicPigGames.Shared
{

public class TextInputDialog : EditorWindow
{
    private string inputText = "";
    private string message = ""; // Field to store the message
    private Action onCancel;
    private Action<string> onOk;

    private void OnGUI()
    {
        Label($"{message}", false, true, true);
        inputText = TextField(inputText);

        GUILayout.FlexibleSpace();

        StartRow();
        if (Button("Cancel"))
        {
            onCancel?.Invoke();
            Close();
        }

        if (Button("OK"))
        {
            onOk?.Invoke(inputText);
            Close();
        }

        EndRow();
    }

    // Method to open the dialog
    public static void ShowDialog(string title, string message, string defaultValue, Action<string> onOk,
        Action onCancel, int width = 500, int height = 200)
    {
        var window = CreateInstance<TextInputDialog>();
        window.titleContent = new GUIContent(title);
        window.inputText = defaultValue;
        window.message = message; // Store the message
        window.onOk = onOk;

        window.onCancel = onCancel;

        window.minSize = new Vector2(width, height);
        window.maxSize = window.minSize;

        // give it some initial size, ShowUtility will cause centering
        window.position = new Rect(0, 0, width, height);
        window.ShowUtility();
    }

    // Helper method to find the main Editor window
    private static EditorWindow GetMainWindow()
    {
        // This is an approximation to find the main window. You might need to adjust this based on your setup.
        var mainWindow = Resources.FindObjectsOfTypeAll<EditorWindow>()
            .FirstOrDefault(window => window.GetType().Name == "MainWindow");
        return mainWindow ?? GetWindow<SceneView>();
    }
}
}
