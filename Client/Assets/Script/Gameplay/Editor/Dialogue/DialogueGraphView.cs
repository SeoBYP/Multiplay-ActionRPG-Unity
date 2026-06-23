using System;
using System.Collections.Generic;
using System.Linq;
using Game.Presentation.Dialogue;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.Gameplay.Editor.Dialogue
{
    /// <summary>
    /// 대화 그래프 캔버스. 노드 추가/드래그 연결/삭제 + DialogueDefinition 으로 직렬화·역직렬화.
    /// 엣지(선택지 출력→노드 입력) = 그 선택지의 targetNodeId. 시작 노드는 ★ 배지로 표시.
    /// </summary>
    public sealed class DialogueGraphView : GraphView
    {
        public string StartNodeId { get; private set; }

        public DialogueGraphView()
        {
            style.flexGrow = 1;
            SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());

            var grid = new GridBackground();
            Insert(0, grid);
            grid.StretchToParentSize();

            // 빈 캔버스 우클릭 → 노드 추가.
            this.AddManipulator(new ContextualMenuManipulator(evt =>
            {
                var local = contentViewContainer.WorldToLocal(evt.mousePosition);
                evt.menu.AppendAction("Add Node", _ => CreateNode(NewId(), local));
            }));
        }

        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
            => ports.ToList().Where(p =>
                p.direction != startPort.direction && p.node != startPort.node).ToList();

        private static string NewId() => Guid.NewGuid().ToString("N");

        private DialogueNodeView CreateNode(string id, Vector2 position)
        {
            var node = new DialogueNodeView(id, OnSetStart, n => n.AddChoice("선택지", DialogueActionKind.GoTo, "", DialogueShowCondition.Always, ""));
            node.SetPosition(new Rect(position, new Vector2(320, 200)));
            AddElement(node);
            return node;
        }

        private void OnSetStart(DialogueNodeView node)
        {
            StartNodeId = node.NodeId;
            UpdateStartBadges();
        }

        private void UpdateStartBadges()
        {
            foreach (var n in nodes.OfType<DialogueNodeView>())
                n.SetStartBadge(n.NodeId == StartNodeId);
        }

        // ── 역직렬화: SO → 그래프 ──
        public void PopulateFrom(DialogueDefinition def)
        {
            DeleteElements(graphElements.ToList());
            if (def == null) return;

            StartNodeId = def.startNodeId;
            var viewById = new Dictionary<string, DialogueNodeView>();

            foreach (var n in def.Nodes)
            {
                if (n == null || string.IsNullOrEmpty(n.id)) continue;
                var view = CreateNode(n.id, n.editorPosition);
                view.ShotField.value = n.shot;
                view.SpeakerField.value = n.speaker ?? string.Empty;
                view.BodyField.value = n.bodyText ?? string.Empty;
                foreach (var c in n.choices)
                    view.AddChoice(c.label, c.action, c.questId, c.showIf, c.conditionQuestId);
                viewById[n.id] = view;
            }

            // 엣지 연결: 각 선택지의 targetNodeId → 대상 노드 입력.
            foreach (var n in def.Nodes)
            {
                if (n == null || !viewById.TryGetValue(n.id, out var view)) continue;
                for (int i = 0; i < n.choices.Count && i < view.Choices.Count; i++)
                {
                    var target = n.choices[i].targetNodeId;
                    if (string.IsNullOrEmpty(target) || !viewById.TryGetValue(target, out var targetView)) continue;
                    var edge = view.Choices[i].Output.ConnectTo(targetView.Input);
                    AddElement(edge);
                }
            }

            UpdateStartBadges();
        }

        // ── 직렬화: 그래프 → SO ──
        public void SaveTo(DialogueDefinition def)
        {
            var list = new List<DialogueNode>();
            foreach (var view in nodes.OfType<DialogueNodeView>())
            {
                var node = new DialogueNode
                {
                    id = view.NodeId,
                    editorPosition = view.GetPosition().position,
                    shot = (Game.System.Dialogue.DialogueShot)view.ShotField.value,
                    speaker = view.SpeakerField.value,
                    bodyText = view.BodyField.value,
                    choices = new List<DialogueChoice>(),
                };

                foreach (var row in view.Choices)
                {
                    var choice = new DialogueChoice
                    {
                        label = row.Label.value,
                        action = (DialogueActionKind)row.Action.value,
                        questId = row.QuestId.value,
                        showIf = (DialogueShowCondition)row.ShowIf.value,
                        conditionQuestId = row.ConditionQuestId.value,
                        targetNodeId = ResolveTarget(row.Output),
                    };
                    node.choices.Add(choice);
                }
                list.Add(node);
            }

            def.startNodeId = StartNodeId;
            def.EditorNodes.Clear();
            def.EditorNodes.AddRange(list);
            EditorUtility.SetDirty(def);
            AssetDatabase.SaveAssets();
        }

        private static string ResolveTarget(Port output)
        {
            var edge = output.connections.FirstOrDefault();
            return edge?.input?.node is DialogueNodeView target ? target.NodeId : string.Empty;
        }
    }
}
