using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.Linq;
using InfinityPBR;
using MagicPigGames.Shared;

namespace InfinityPBR
{
    [CustomEditor(typeof(BlendShapesPresetManager))]
    [CanEditMultipleObjects]
    [Serializable]
    public class BlendShapesPresetManagerEditor : MagicPigEditor
    {
        private Color inactiveColor2 = new Color(0.75f, .75f, 0.75f, 1f);
        private Color activeColor = new Color(0.6f, 1f, 0.6f, 1f);
        private Color activeColor2 = new Color(0.0f, 1f, 0.0f, 1f);
        private Color mixedColor = Color.yellow;
        private Color redColor = new Color(1f, 0.25f, 0.25f, 1f);

        private BlendShapesPresetManager Manager => GetManager();
        private BlendShapesPresetManager _blendShapesPresetManager;
        private BlendShapesManager BlendShapesManager => Manager.BlendShapesManager;

        private bool _somethingChanged = false;
        private float _reportTime = -1;
        
        private BlendShapesPresetManager GetManager()
        {
            if (_blendShapesPresetManager != null) return _blendShapesPresetManager;
            _blendShapesPresetManager = (BlendShapesPresetManager) target;
            return _blendShapesPresetManager;
        }

        public override void OnInspectorGUI()
        {
            if (Manager.showHelpBoxes)
            {
                EditorGUILayout.HelpBox("BLEND SHAPE PRESET MANAGER\n" +
                                        "Use this script with BlendShapesManager.cs to create groups of preset shapes, which can " +
                                        "be called with a single line of code. For example, you may wish to create a \"Strong\" or " +
                                        "a \"Weak\" version of a character, or have multiple face settings.\n\nYou can also set some " +
                                        "shapes to randomize on load, which will allow you to create random character looks whenever " +
                                        "one is instantiated.", MessageType.None);
            }

            ShowSetupAndOptions();

            DrawLine();

            ShowButtons();

            ShowPresetList();

            ShowDefaultInspector();
            
            if (_reportTime > 0)
            {
                Debug.Log($"TOTAL TIME took {Time.realtimeSinceStartup - _reportTime} seconds");
                _reportTime = -1;
            }
            
            if (_somethingChanged)
            {
                _somethingChanged = false;
                _reportTime = Time.realtimeSinceStartup;
                Debug.Log($"Setting Dirty at {_reportTime}");
                //EditorUtility.SetDirty(Manager);
                Debug.Log($"SetDirty took {Time.realtimeSinceStartup - _reportTime} seconds");
            }
            
        }

        private void ShowDefaultInspector()
        {
            if (!Manager.showFullInspector) return;

            DrawLine();
            EditorGUILayout.Space();
            DrawDefaultInspector();
        }

        private void ShowPresetList()
        {
            // DISPLAY LIST
            for (int i = 0; i < Manager.presets.Count; i++)
            {
                BlendShapePreset preset = Manager.presets[i];
                ShowPreset(preset, i);
            }
        }

        private void ShowPreset(BlendShapePreset preset, int presetIndex)
        {
            GUI.backgroundColor = preset.showValues ? activeColor : Color.white;
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(preset.showValues ? "o" : "=", GUILayout.Width(20)))
            {
                preset.showValues = !preset.showValues;
            }

            EditorGUILayout.LabelField("Preset Name", GUILayout.Width(150));
            var tempName = preset.name;
            preset.name = EditorGUILayout.DelayedTextField(preset.name);
            if (tempName != preset.name)
            {
                EditorUtility.SetDirty(Manager);
                Debug.Log("Name was updated");
            }
            if (GUILayout.Button("Activate", GUILayout.Width(150)))
            {
                Manager.ActivatePreset(presetIndex);
            }

            if (GUILayout.Button("Copy", GUILayout.Width(150)))
            {
                CopyPreset(Manager, presetIndex);
            }

            EditorGUILayout.EndHorizontal();


            if (preset.showValues)
            {
                EditorGUI.indentLevel++;
                for (int v = 0; v < preset.presetValues.Count; v++)
                {

                    BlendShapePresetValue value = preset.presetValues[v];
                    EditorGUILayout.BeginHorizontal();
                    GUI.backgroundColor = redColor;
                    if (GUILayout.Button("x", GUILayout.Width(20)))
                    {
                        Undo.RecordObject(Manager, "Undo Record");
                        preset.presetValues.RemoveAt(v);
                        GUI.backgroundColor = preset.showValues ? activeColor : Color.white;
                        //SomethingChanged();
                        EditorUtility.SetDirty(Manager);
                    }
                    else
                    {

                        GUI.backgroundColor = preset.showValues ? activeColor : Color.white;
                        EditorGUILayout.LabelField(value.objectName + " " + value.valueTriggerName);

                        Undo.RecordObject(Manager, "Undo Record");
                        value.onTriggerModeIndex =
                            EditorGUILayout.Popup(value.onTriggerModeIndex, Manager.onTriggerMode);
                        value.onTriggerMode = Manager.onTriggerMode[value.onTriggerModeIndex];
                        if (value.onTriggerMode == "Explicit")
                        {
                            Undo.RecordObject(Manager, "Undo Record");
                            value.shapeValue = EditorGUILayout.Slider(value.shapeValue, value.min, value.max);
                        }

                        if (value.onTriggerMode == "Random")
                        {
                            //EditorGUILayout.LabelField("This value will be randomized.");
                        }

                    }

                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Add new shape", GUILayout.Width(150));
                Manager.shapeListIndex = EditorGUILayout.Popup(Manager.shapeListIndex, Manager.shapeListNames);
                if (GUILayout.Button("Add Blendshape"))
                {
                    AddNewPresetValue(Manager, preset);
                }

                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();


                if (Button("Add all", 50))
                {
                    AddAllBlendshapes(Manager, preset);
                }
                Space();

                Label(" ", 30);
                if (Button("Set all Explicit", 150))
                {
                    for (int v = 0; v < preset.presetValues.Count; v++)
                    {
                        preset.presetValues[v].onTriggerModeIndex = 0;
                        preset.presetValues[v].onTriggerMode = "Explicit";
                    }
                    //SomethingChanged();
                    EditorUtility.SetDirty(Manager);
                }

                if (Button("Set all Random", 150))
                {
                    for (int v = 0; v < preset.presetValues.Count; v++)
                    {
                        preset.presetValues[v].onTriggerModeIndex = 1;
                        preset.presetValues[v].onTriggerMode = "Random";
                    }
                    //SomethingChanged();
                    EditorUtility.SetDirty(Manager);
                }

                if (Button("Set values = 0", 150))
                {
                    for (int v = 0; v < preset.presetValues.Count; v++)
                    {
                        preset.presetValues[v].shapeValue = 0f;
                    }
                    //SomethingChanged();
                    EditorUtility.SetDirty(Manager);
                }

                EditorGUILayout.EndHorizontal();

                DrawLine();
                GUI.backgroundColor = redColor;
                if (GUILayout.Button("Remove This Preset"))
                {
                    Manager.presets.RemoveAt(presetIndex);
                    //SomethingChanged();
                    EditorUtility.SetDirty(Manager);
                }

                GUI.backgroundColor = preset.showValues ? activeColor : Color.white;
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
        }

        private void ShowButtons()
        {
            EditorGUILayout.BeginHorizontal();

            ColorsIf(Manager.shapeList.Count > 0, Color.white, Color.black, Color.white, Color.grey);
            if (Manager.shapeList.Count > 0)
                BackgroundColor(Manager.presets.Count == 0 ? Color.green : Color.white);
            if (GUILayout.Button($"Add new preset group [{Manager.presets.Count}]"))
            {
                if (Manager.shapeList.Count > 0)
                    AddNewPresetGroup(Manager);
            }
            ResetColor();
            

            BackgroundColorIf(Manager.shapeList.Count == 0, Color.green, Color.white);
            if (GUILayout.Button($"Reload Blendshape List [{Manager.shapeList.Count}]"))
                BuildShapeList(BlendShapesManager, Manager);
            ResetColor();
            

            EditorGUILayout.EndHorizontal();
        }

        private void ShowSetupAndOptions()
        {
            Manager.showSetup = EditorGUILayout.Foldout(Manager.showSetup, "Setup & Options");
            if (Manager.showSetup)
            {
                EditorGUI.indentLevel++;
                Manager.showHelpBoxes = EditorGUILayout.Toggle("Show help boxes", Manager.showHelpBoxes);
                Manager.showFullInspector = EditorGUILayout.Toggle("Show full inspector", Manager.showFullInspector);
                Manager.lerpSeconds = EditorGUILayout.FloatField("Transition Seconds", Manager.lerpSeconds);

                EditorGUI.indentLevel--;
            }
        }

        private void BuildShapeList(BlendShapesManager blendShapesManager, BlendShapesPresetManager presetManager)
        {
            var startTime = Time.realtimeSinceStartup;
            //Debug.Log($"Starting BuildShapeList at {startTime}");
            
            presetManager.shapeList.Clear();
            presetManager.shapeListIndex = 0;
            
            foreach (var obj in blendShapesManager.blendShapeGameObjects)
            {
                if (obj.displayableValues == 0)
                    continue;
                if (!obj.gameObject)
                    continue;
                
                foreach (var value in obj.blendShapeValues)
                {
                    if (!value.display)
                        continue;
                    if (value.matchAnotherValue)
                        continue;
                    if (value.otherValuesMatchThis.Count > 0)
                        continue;

                    presetManager.shapeList.Add(new Shape { obj = obj, value = value });
                }
            }
            
            presetManager.shapeListNames = new string[presetManager.shapeList.Count];
            for (var i = 0; i < presetManager.shapeList.Count; i++)
            {
                var gameObjectName = presetManager.shapeList[i].obj.gameObjectName;
                var triggerName = presetManager.shapeList[i].value.triggerName;
                presetManager.shapeListNames[i] = $"{gameObjectName} {triggerName}";
            }

            UpdatePresetLimits(presetManager);
            
            //SomethingChanged();
            EditorUtility.SetDirty(Manager);
            //Debug.Log("END");
            //Debug.Log($"TOTAL TIME: {Time.realtimeSinceStartup - startTime} seconds. There are {presetManager.shapeList.Count} shapes in the list.");
            
        }

        private void SomethingChanged() => _somethingChanged = true;

        private void UpdatePresetLimits(BlendShapesPresetManager presetManager)
        {
            //Debug.Log($"Will go through {presetManager.presets.Count} presets");
            foreach (var preset in presetManager.presets)
            {
                //Debug.Log($"Will go through {preset.presetValues.Count} values");
                foreach (var value in preset.presetValues)
                {
                    
                    var shapeValue = BlendShapesManager.GetBlendShapeValue(value.objectName, value.valueTriggerName);
                    value.limitMin = shapeValue.limitMin;
                    value.limitMax = shapeValue.limitMax;
                }
            }
        }

        private void AddAllBlendshapes(BlendShapesPresetManager presetManager, BlendShapePreset preset)
        {
            foreach(var shape in presetManager.shapeList)
            {
                if (preset.presetValues.FirstOrDefault(x => x.objectName == shape.obj.gameObjectName && x.valueTriggerName == shape.value.triggerName) != null) continue;
                
                var newValue = new BlendShapePresetValue
                {
                    objectName = shape.obj.gameObjectName,
                    valueTriggerName = shape.value.triggerName,
                    limitMin = shape.value.limitMin,
                    limitMax = shape.value.limitMax,
                    min = shape.value.min,
                    max = shape.value.max,
                    shapeValue = shape.value.value
                };
                preset.presetValues.Add(newValue);
            }
            EditorUtility.SetDirty(Manager);
            //SomethingChanged();
        }

        private void AddNewPresetValue(BlendShapesPresetManager presetManager, BlendShapePreset preset)
        {
            preset.presetValues.Add(new BlendShapePresetValue());
            var newValue = preset.presetValues[preset.presetValues.Count - 1];

            var shape = presetManager.shapeList[presetManager.shapeListIndex];

            newValue.objectName = shape.obj.gameObjectName;
            newValue.valueTriggerName = shape.value.triggerName;
            newValue.limitMin = shape.value.limitMin;
            newValue.limitMax = shape.value.limitMax;
            newValue.min = shape.value.min;
            newValue.max = shape.value.max;
            newValue.shapeValue = shape.value.value;
            EditorUtility.SetDirty(presetManager);
            //SomethingChanged();
        }

        private void AddAllPresetValues(BlendShapesPresetManager presetManager, BlendShapePreset preset)
        {
            preset.presetValues.Add(new BlendShapePresetValue());
            var newValue = preset.presetValues[preset.presetValues.Count - 1];

            foreach (var shape in presetManager.shapeList)
            {
                newValue.objectName = shape.obj.gameObjectName;
                newValue.valueTriggerName = shape.value.triggerName;
                newValue.limitMin = shape.value.limitMin;
                newValue.limitMax = shape.value.limitMax;
                newValue.min = shape.value.min;
                newValue.max = shape.value.max;
            }
            EditorUtility.SetDirty(presetManager);
        }

        private void AddNewPresetGroup(BlendShapesPresetManager presetManager)
        {
            var newPreset = new BlendShapePreset
            {
                name = "New Preset Group"
            };

            //AddAllPresetValues(Manager, newPreset);
            
            presetManager.presets.Add(newPreset);
            //SomethingChanged();
            EditorUtility.SetDirty(presetManager);
        }

        private void CopyPreset(BlendShapesPresetManager presetManager, int presetIndex)
        {
            AddNewPresetGroup(presetManager);
            BlendShapePreset copyFrom = presetManager.presets[presetIndex];
            BlendShapePreset copyTo = presetManager.presets[presetManager.presets.Count - 1];
            copyTo.name = copyFrom.name + " Copy";
            copyTo.presetValues = new List<BlendShapePresetValue>();
            for (int v = 0; v < copyFrom.presetValues.Count; v++)
            {
                copyTo.presetValues.Add(new BlendShapePresetValue());
                copyTo.presetValues[v].max = copyFrom.presetValues[v].max;
                copyTo.presetValues[v].min = copyFrom.presetValues[v].min;
                copyTo.presetValues[v].limitMax = copyFrom.presetValues[v].limitMax;
                copyTo.presetValues[v].limitMin = copyFrom.presetValues[v].limitMin;
                copyTo.presetValues[v].shapeValue = copyFrom.presetValues[v].shapeValue;
                copyTo.presetValues[v].objectName = copyFrom.presetValues[v].objectName;
                copyTo.presetValues[v].onTriggerMode = copyFrom.presetValues[v].onTriggerMode;
                copyTo.presetValues[v].valueTriggerName = copyFrom.presetValues[v].valueTriggerName;
                copyTo.presetValues[v].onTriggerModeIndex = copyFrom.presetValues[v].onTriggerModeIndex;
            }

            copyTo.showValues = copyFrom.showValues;
            //SomethingChanged();
            EditorUtility.SetDirty(presetManager);
        }

        void DrawLine(bool spacers = true, int height = 1)
        {
            if (spacers)
                EditorGUILayout.Space();
            Rect rect = EditorGUILayout.GetControlRect(false, height);
            rect.height = height;
            EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 1));
            if (spacers)
                EditorGUILayout.Space();
        }
    }
}