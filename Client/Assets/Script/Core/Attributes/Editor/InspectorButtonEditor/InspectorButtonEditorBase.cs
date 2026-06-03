#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Game.Core.Editor
{
    public class InspectorButtonEditorBase : UnityEditor.Editor
    {
        private const BindingFlags MethodFlags =
            BindingFlags.Instance |
            BindingFlags.Static |
            BindingFlags.Public |
            BindingFlags.NonPublic;

        private readonly List<MethodInfo> _buttonMethods = new();

        protected virtual void OnEnable()
        {
            CacheButtonMethods();
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            if (_buttonMethods.Count == 0)
                return;

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Inspector Buttons", EditorStyles.boldLabel);

            foreach (MethodInfo method in _buttonMethods)
            {
                DrawButton(method);
            }
        }

        private void CacheButtonMethods()
        {
            _buttonMethods.Clear();

            Type type = target.GetType();

            while (type != null && type != typeof(MonoBehaviour) && type != typeof(ScriptableObject))
            {
                MethodInfo[] methods = type.GetMethods(MethodFlags);

                foreach (MethodInfo method in methods)
                {
                    if (method.GetCustomAttribute<InspectorButtonAttribute>(true) == null)
                        continue;

                    if (method.GetParameters().Length > 0)
                    {
                        Debug.LogWarning(
                            $"[InspectorButton] '{type.Name}.{method.Name}' is ignored. " +
                            "InspectorButton methods must have no parameters.",
                            target
                        );
                        continue;
                    }

                    if (_buttonMethods.Contains(method))
                        continue;

                    _buttonMethods.Add(method);
                }

                type = type.BaseType;
            }
        }

        private void DrawButton(MethodInfo method)
        {
            var attribute = method.GetCustomAttribute<InspectorButtonAttribute>(true);

            using (new EditorGUI.DisabledScope(attribute.PlayModeOnly && !Application.isPlaying))
            {
                string label = string.IsNullOrWhiteSpace(attribute.Label)
                    ? ObjectNames.NicifyVariableName(method.Name)
                    : attribute.Label;

                if (!GUILayout.Button(label))
                    return;

                InvokeMethodForTargets(method);
            }
        }

        private void InvokeMethodForTargets(MethodInfo method)
        {
            foreach (UnityEngine.Object selectedTarget in targets)
            {
                if (selectedTarget == null)
                    continue;

                Undo.RecordObject(selectedTarget, $"Invoke {method.Name}");

                try
                {
                    method.Invoke(selectedTarget, null);

                    EditorUtility.SetDirty(selectedTarget);
                }
                catch (TargetInvocationException e)
                {
                    Debug.LogException(e.InnerException ?? e, selectedTarget);
                }
                catch (Exception e)
                {
                    Debug.LogException(e, selectedTarget);
                }
            }
        }
    }
}
#endif