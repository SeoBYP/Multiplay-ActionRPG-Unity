using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

namespace MagicPigGames.Shared
{
    public class ComponentRemover : EditorWindow
    {
        private Dictionary<string, int> componentCounts;
        private Dictionary<string, System.Type> componentTypes;
        private const string PromptBeforeRemovalKey = "ComponentRemover_PromptBeforeRemoval";
        private bool promptBeforeRemoval;
        private bool hasMissingComponents = false;

        [MenuItem("Window/Magic Pig Games/Component Remover")]
        private static void Init()
        {
            var window = (ComponentRemover)GetWindow(typeof(ComponentRemover));
            window.Show();
        }

        [MenuItem("Window/Magic Pig Games/Component Remover", true)]
        private static bool ValidateMenu()
        {
            return Selection.activeObject != null;
        }

        private void OnEnable()
        {
            RefreshComponentCounts();
            promptBeforeRemoval = EditorPrefs.GetBool(PromptBeforeRemovalKey, true);
            Undo.undoRedoPerformed += OnUndoRedoPerformed;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedoPerformed;
        }

        private void OnSelectionChange()
        {
            RefreshComponentCounts();
            Repaint();
        }

        private void OnUndoRedoPerformed()
        {
            RefreshComponentCounts();
            Repaint();
        }

        private void RefreshComponentCounts()
        {
            hasMissingComponents = false;
            componentCounts = new Dictionary<string, int>();
            componentTypes = new Dictionary<string, System.Type>();

            foreach (var obj in Selection.objects)
            {
                if (obj is GameObject gameObject)
                {
                    AddComponentsFromGameObject(gameObject);
                }
            }
        }

        private void AddComponentsFromGameObject(GameObject gameObject)
        {
            var components = gameObject.GetComponentsInChildren<Component>(true);
            foreach (var component in components)
            {
                if (component == null)
                {
                    hasMissingComponents = true;
                    continue;
                } // Skip missing components}

                var type = component.GetType();
                var typeName = type.Name;

                if (!componentCounts.ContainsKey(typeName))
                {
                    
                    componentCounts[typeName] = 0;
                    componentTypes[typeName] = type;
                }
                
                componentCounts[typeName]++;
            }
        }

        private void OnGUI()
        {
            GUILayout.Label("Component Remover", EditorStyles.boldLabel);
            
            if (hasMissingComponents)
            {
                GUILayout.Label("One or more objects have missing components. You can use Window/Magic Pig Games/Remove Missing Components to remove them.\n\nAfter, reselect the objects to run this again.", EditorStyles.wordWrappedLabel);
                return;
            }

            if (componentCounts == null || componentCounts.Count == 0)
            {
                GUILayout.Label("No components found on the selected objects.", EditorStyles.wordWrappedLabel);
                return;
            }

            foreach (var componentEntry in componentCounts)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{componentEntry.Key} [{componentEntry.Value}]", GUILayout.Width(200));

                if (GUILayout.Button("Remove", GUILayout.Width(100)))
                {
                    if (promptBeforeRemoval)
                    {
                        if (EditorUtility.DisplayDialog("Confirm Removal",
                            "Are you sure you want to proceed? You can \"Undo\" this action.", "Yes", "No"))
                        {
                            RemoveComponent(componentTypes[componentEntry.Key]);
                        }
                    }
                    else
                    {
                        RemoveComponent(componentTypes[componentEntry.Key]);
                    }
                }

                GUILayout.EndHorizontal();
            }
            
            GUILayout.Space(20);

            promptBeforeRemoval = EditorGUILayout.Toggle("Prompt Before Removal", promptBeforeRemoval);
            EditorPrefs.SetBool(PromptBeforeRemovalKey, promptBeforeRemoval);
        }

        private void RemoveComponent(System.Type componentType)
        {
            foreach (var obj in Selection.objects)
            {
                if (obj is GameObject gameObject)
                {
                    var components = gameObject.GetComponentsInChildren(componentType, true);
                    foreach (var component in components)
                    {
                        Undo.RegisterCompleteObjectUndo(gameObject, "Remove Component");
                        Undo.DestroyObjectImmediate(component);
                    }
                }
            }

            RefreshComponentCounts();
        }
    }
}
