using System.Collections.Generic;
using System.Linq;
using Game.Presentation.Dialogue;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.Gameplay.Editor.Dialogue
{
    /// <summary>
    /// 대화 그래프 저작 윈도우(A2). DialogueDefinition 에셋을 노드 그래프로 편집한다.
    /// 열기: 에셋 더블클릭 / 메뉴 Tools ▸ Dialogue ▸ Graph Editor. [Save]로 SO에 직렬화.
    /// </summary>
    public sealed class DialogueGraphWindow : EditorWindow
    {
        private DialogueGraphView _graph;
        private DialogueDefinition _target;
        private ObjectField _targetField;
        private TextField _hideField;

        [MenuItem("Tools/Dialogue/Graph Editor")]
        public static void Open()
        {
            var win = GetWindow<DialogueGraphWindow>();
            win.titleContent = new GUIContent("Dialogue Graph");
        }

        /// <summary>DialogueDefinition 에셋 더블클릭 → 이 윈도우로 연다.</summary>
        [OnOpenAsset]
        public static bool OnOpenAsset(int instanceId, int line)
        {
            if (EditorUtility.InstanceIDToObject(instanceId) is not DialogueDefinition def) return false;
            var win = GetWindow<DialogueGraphWindow>();
            win.titleContent = new GUIContent("Dialogue Graph");
            win.Load(def);
            return true;
        }

        private void CreateGUI()
        {
            _graph = new DialogueGraphView();
            BuildToolbar();
            rootVisualElement.Add(_graph);
            if (_target != null) _graph.PopulateFrom(_target);
        }

        private void BuildToolbar()
        {
            var toolbar = new Toolbar();

            _targetField = new ObjectField("Dialogue") { objectType = typeof(DialogueDefinition), value = _target };
            _targetField.RegisterValueChangedCallback(evt => Load(evt.newValue as DialogueDefinition));

            var saveBtn = new ToolbarButton(Save) { text = "Save" };
            var reloadBtn = new ToolbarButton(() => { if (_target != null) _graph.PopulateFrom(_target); }) { text = "Reload" };

            // 대화 중 숨길 오브젝트(쉼표 구분 이름/경로). Save 시 def.hideObjects 로 직렬화.
            _hideField = new TextField("숨길 오브젝트") { value = HideObjectsToText(_target) };
            _hideField.tooltip = "대화 동안 비활성화할 GameObject 이름(쉼표 구분). 예: GameHud  또는  Canvas/GameHud";
            _hideField.style.minWidth = 240;

            toolbar.Add(_targetField);
            toolbar.Add(saveBtn);
            toolbar.Add(reloadBtn);
            toolbar.Add(_hideField);
            rootVisualElement.Add(toolbar);
        }

        private void Load(DialogueDefinition def)
        {
            _target = def;
            if (_targetField != null) _targetField.SetValueWithoutNotify(def);
            if (_hideField != null) _hideField.SetValueWithoutNotify(HideObjectsToText(def));
            if (_graph != null) _graph.PopulateFrom(def);
        }

        private void Save()
        {
            if (_target == null)
            {
                EditorUtility.DisplayDialog("Dialogue Graph", "저장할 DialogueDefinition 을 먼저 지정하세요.", "확인");
                return;
            }
            _target.hideObjects = ParseHideObjects(_hideField != null ? _hideField.value : null);
            _graph.SaveTo(_target);   // SetDirty + SaveAssets (hideObjects 포함 저장)
        }

        private static string HideObjectsToText(DialogueDefinition def)
            => def != null && def.hideObjects != null ? string.Join(", ", def.hideObjects) : string.Empty;

        private static List<string> ParseHideObjects(string text)
            => string.IsNullOrWhiteSpace(text)
                ? new List<string>()
                : text.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
    }
}
