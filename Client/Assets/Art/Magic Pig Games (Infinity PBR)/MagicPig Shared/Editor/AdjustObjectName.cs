using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static MagicPigGames.Shared.MagicPigEditor;
using Space = UnityEngine.Space;

namespace MagicPigGames.Shared
{
    public class AdjustObjectName : EditorWindow
    {
        private bool _addSpacesBeforeCamelCase, _replaceUnderscoresWithSpaces;
        private string _namePrefix = "", _nameSuffix = "";
        private string _replaceFrom = "", _replaceTo = "";
        private bool _replaceWholeWordsOnly = true;

        private void OnGUI()
        {
            GUILayout.Label("Name Adjustment Settings", EditorStyles.boldLabel);
            _namePrefix = EditorGUILayout.TextField("Name Prefix", _namePrefix);
            _nameSuffix = EditorGUILayout.TextField("Name Suffix", _nameSuffix);

            Space();
            StartRow();
            Label($"Replace", 75);
            _replaceFrom = TextField(_replaceFrom, 50);
            Label($"With", 50);
            _replaceTo = TextField(_replaceTo, 50);
            EndRow();
            _replaceWholeWordsOnly = LeftCheck("Replace Whole Words Only", _replaceWholeWordsOnly);
            
            Space();
            _addSpacesBeforeCamelCase =
                EditorGUILayout.Toggle("Add Spaces Between Camel Case", _addSpacesBeforeCamelCase);
            _replaceUnderscoresWithSpaces =
                EditorGUILayout.Toggle("Replace Underscores With Spaces", _replaceUnderscoresWithSpaces);
            if (!GUILayout.Button("Apply Changes")) return;


            float total = Selection.objects.Length; // capture the total number of objects

            for (var i = 0; i < total; i++) // iterate over all selected objects using index
            {
                var selectedObject = Selection.objects[i];
                if (selectedObject is GameObject gameObject) // If the selected object is a GameObject
                {
                    if (gameObject.scene.IsValid())
                        UpdateNameForSceneObject(gameObject);
                    else
                        UpdateName(gameObject); // In Project game object
                }
                else if
                    (selectedObject is ScriptableObject
                     scriptableObject) // If the selected object is a ScriptableObject
                {
                    UpdateName(scriptableObject);
                }
                else
                {
                    Debug.LogError("Selected object is neither a GameObject nor a ScriptableObject");
                    continue; // skip this iteration and move to the next object
                }

                var progress = i / total; // calculate progress
                var progressMessage = $"Processing {i + 1} of {total}: {selectedObject.name}";
                EditorUtility.DisplayProgressBar("Adjusting names...", progressMessage,
                    progress); // display the progress bar
            }

            EditorUtility.ClearProgressBar(); // clear the progress bar when the updates are done
        }

        [MenuItem("Window/Magic Pig Games/Adjust Object Names")]
        private static void Init()
        {
            var window = (AdjustObjectName)GetWindow(typeof(AdjustObjectName));
            window.Show();
        }

        [MenuItem("Window/Magic Pig Games/Adjust Object Names", true)]
        private static bool ValidateMenu()
        {
            return Selection.activeObject != null;
            // Updated to handle more than just GameObjects
        }

        private void UpdateNameForSceneObject(GameObject obj)
        {
            var oldName = obj.name;
            var newName = FormatName(oldName);
            obj.name = newName; // Directly change the name of the in-scene GameObject
            EditorSceneManager.MarkSceneDirty(obj.scene); // Mark the scene as dirty for changes
            Debug.Log($"Old Name: {oldName}, New Name: {newName}");
        }

        private void UpdateName(GameObject obj)
        {
            var oldName = obj.name;
            var newName = FormatName(oldName);

            if (PrefabUtility.GetPrefabAssetType(obj) == PrefabAssetType.NotAPrefab)
            {
                // If the object is a GameObject in the scene
                obj.name = newName;
                EditorSceneManager.MarkSceneDirty(obj.scene);
            }
            else
            {
                // If the object is a GameObject in the project
                var path = AssetDatabase.GetAssetPath(obj);
                AssetDatabase.RenameAsset(path, newName);
            }

            Debug.Log($"Old Name: {oldName}, New Name: {newName}");
        }

        private void UpdateName(ScriptableObject obj)
        {
            var oldName = obj.name;
            var newName = FormatName(oldName);

            // For ScriptableObjects, we only need to handle renaming the asset in the project
            var path = AssetDatabase.GetAssetPath(obj);
            AssetDatabase.RenameAsset(path, newName);

            Debug.Log($"Old Name: {oldName}, New Name: {newName}");
        }

        private string FormatName(string originalName)
        {
            var newName = originalName;
            if (_addSpacesBeforeCamelCase)
                newName = Regex.Replace(newName, "(\\B[A-Z])", " $1");
            if (_replaceUnderscoresWithSpaces)
                newName = newName.Replace('_', ' ');

            // Added replace logic
            if (!string.IsNullOrEmpty(_replaceFrom))
            {
                string pattern = _replaceWholeWordsOnly ? $@"\b{_replaceFrom}\b" : _replaceFrom;
                newName = Regex.Replace(newName, pattern, _replaceTo, RegexOptions.IgnoreCase);
            }

            if (!string.IsNullOrEmpty(_namePrefix))
                newName = _namePrefix + newName;
            if (!string.IsNullOrEmpty(_nameSuffix))
                newName = newName + _nameSuffix;
            return newName;
        }
    }
}