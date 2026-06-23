using System;
using System.Collections.Generic;
using Game.Presentation.Dialogue;
using Game.System.Dialogue;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.Gameplay.Editor.Dialogue
{
    /// <summary>
    /// 그래프 한 노드(=DialogueNode). 입력 포트 1 + 화자/본문 필드 + 선택지마다 행(라벨·액션·조건·출력포트).
    /// GoTo 선택지의 출력 포트를 다른 노드 입력 포트에 연결하면 targetNodeId 가 된다(저장 시 엣지→id 변환).
    /// </summary>
    public sealed class DialogueNodeView : Node
    {
        public string NodeId { get; }
        public Port Input { get; }
        public EnumField ShotField { get; }
        public TextField SpeakerField { get; }
        public TextField BodyField { get; }

        public readonly List<ChoiceRow> Choices = new();

        private readonly VisualElement _choiceContainer;
        private readonly Label _startBadge;

        public sealed class ChoiceRow
        {
            public VisualElement Root;
            public TextField Label;
            public EnumField Action;       // DialogueActionKind
            public TextField QuestId;      // Accept/ClaimQuest
            public EnumField ShowIf;       // DialogueShowCondition
            public TextField ConditionQuestId;
            public Port Output;            // GoTo 대상 연결
        }

        public DialogueNodeView(string nodeId, Action<DialogueNodeView> onSetStart, Action<DialogueNodeView> onAddChoice)
        {
            NodeId = nodeId;
            title = "Dialogue";

            _startBadge = new Label("★ START") { style = { color = new StyleColor(new Color(1f, 0.8f, 0.2f)), unityFontStyleAndWeight = FontStyle.Bold } };
            _startBadge.style.display = DisplayStyle.None;
            titleContainer.Add(_startBadge);

            // 입력 포트(여러 선택지가 가리킬 수 있게 Multi).
            Input = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(bool));
            Input.portName = "In";
            inputContainer.Add(Input);

            ShotField = new EnumField("Shot", DialogueShot.Closeup);
            SpeakerField = new TextField("Speaker") { value = string.Empty };
            BodyField = new TextField("Body") { multiline = true };
            BodyField.style.minHeight = 48;
            mainContainer.Add(ShotField);
            mainContainer.Add(SpeakerField);
            mainContainer.Add(BodyField);

            _choiceContainer = new VisualElement();
            mainContainer.Add(_choiceContainer);

            var addChoiceBtn = new Button(() => onAddChoice?.Invoke(this)) { text = "+ Choice" };
            var setStartBtn = new Button(() => onSetStart?.Invoke(this)) { text = "Set as Start" };
            var buttons = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            buttons.Add(addChoiceBtn);
            buttons.Add(setStartBtn);
            mainContainer.Add(buttons);

            RefreshExpandedState();
            RefreshPorts();
        }

        public void SetStartBadge(bool isStart)
            => _startBadge.style.display = isStart ? DisplayStyle.Flex : DisplayStyle.None;

        /// <summary>선택지 행 추가. 반환된 Output 포트가 GoTo 엣지 연결점.</summary>
        public ChoiceRow AddChoice(string label, DialogueActionKind action, string questId,
            DialogueShowCondition showIf, string conditionQuestId)
        {
            var row = new ChoiceRow { Root = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 2 } } };

            row.Label = new TextField { value = label ?? string.Empty, style = { minWidth = 90 } };
            row.Action = new EnumField(action) { style = { minWidth = 90 } };
            row.QuestId = new TextField { value = questId ?? string.Empty, style = { minWidth = 90 } };
            row.QuestId.tooltip = "Accept/ClaimQuest 대상 questId";
            row.ShowIf = new EnumField(showIf) { style = { minWidth = 90 } };
            row.ConditionQuestId = new TextField { value = conditionQuestId ?? string.Empty, style = { minWidth = 90 } };
            row.ConditionQuestId.tooltip = "showIf=QuestStatus* 대상 questId";

            var removeBtn = new Button(() =>
            {
                _choiceContainer.Remove(row.Root);
                Choices.Remove(row);
                if (row.Output != null) outputContainer.Remove(row.Output);
                RefreshPorts();
            }) { text = "x" };

            row.Output = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(bool));
            row.Output.portName = "→";

            row.Root.Add(row.Label);
            row.Root.Add(row.Action);
            row.Root.Add(row.QuestId);
            row.Root.Add(row.ShowIf);
            row.Root.Add(row.ConditionQuestId);
            row.Root.Add(removeBtn);
            _choiceContainer.Add(row.Root);
            outputContainer.Add(row.Output);

            Choices.Add(row);
            RefreshExpandedState();
            RefreshPorts();
            return row;
        }
    }
}
