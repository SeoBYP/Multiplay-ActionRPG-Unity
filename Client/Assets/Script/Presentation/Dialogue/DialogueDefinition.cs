using System;
using System.Collections.Generic;
using Game.System.Dialogue;
using UnityEngine;

namespace Game.Presentation.Dialogue
{
    /// <summary>
    /// 한 NPC(또는 대화 단위)의 대화 그래프(ScriptableObject). 그래프 저작툴(A2)이 읽고 쓴다.
    /// 노드 = 본문 + 선택지[], 선택지 = 라벨 + 액션(GoTo/EndDialogue/AcceptQuest/ClaimQuest) + 노출조건.
    /// nodeId 는 안정 GUID(이름 바꿔도 엣지 안 깨짐). editorPosition 은 그래프 레이아웃 보존용(런타임 무시).
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Dialogue/Dialogue Definition", fileName = "Dialogue")]
    public sealed class DialogueDefinition : ScriptableObject
    {
        [Tooltip("대화 시작 노드 id(GUID).")]
        public string startNodeId;

        [Tooltip("대화가 열린 동안 비활성화할 씬 GameObject 이름(또는 'Canvas/GameHud' 경로). 종료 시 복원. 그래프툴 툴바에서 편집.")]
        public List<string> hideObjects = new();

        [SerializeField] private List<DialogueNode> nodes = new();

        public IReadOnlyList<DialogueNode> Nodes => nodes;

        private Dictionary<string, DialogueNode> _byId;

        /// <summary>노드 id 로 조회. 없으면 null.</summary>
        public DialogueNode GetNode(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId)) return null;
            _byId ??= BuildIndex();
            return _byId.GetValueOrDefault(nodeId);
        }

        /// <summary>시작 노드(없으면 첫 노드).</summary>
        public DialogueNode GetStartNode()
        {
            var start = GetNode(startNodeId);
            if (start != null) return start;
            return nodes.Count > 0 ? nodes[0] : null;
        }

        private Dictionary<string, DialogueNode> BuildIndex()
        {
            var dict = new Dictionary<string, DialogueNode>(nodes.Count);
            foreach (var n in nodes)
                if (n != null && !string.IsNullOrEmpty(n.id))
                    dict[n.id] = n;
            return dict;
        }

#if UNITY_EDITOR
        /// <summary>그래프 저작툴(A2) 전용 — 노드 리스트 직접 편집.</summary>
        public List<DialogueNode> EditorNodes => nodes;
#endif
    }

    /// <summary>대화 노드 한 개.</summary>
    [Serializable]
    public sealed class DialogueNode
    {
        public string id;                       // 안정 GUID
        public Vector2 editorPosition;          // 그래프 레이아웃(런타임 무시)
        public DialogueShot shot = DialogueShot.Closeup; // 이 노드의 카메라 구도(그래프툴에서 선택)
        public string speaker;
        [TextArea(2, 5)] public string bodyText;
        public List<DialogueChoice> choices = new();
    }

    /// <summary>노드의 선택지 한 개.</summary>
    [Serializable]
    public sealed class DialogueChoice
    {
        public string label;
        public DialogueActionKind action = DialogueActionKind.GoTo;
        public string targetNodeId;             // action=GoTo 일 때 대상 노드
        public string questId;                  // action=AcceptQuest/ClaimQuest (Phase B)

        // 노출 조건(Phase B) — A1 은 Always 만 평가.
        public DialogueShowCondition showIf = DialogueShowCondition.Always;
        public string conditionQuestId;         // showIf=QuestStatus* 일 때 대상 퀘스트
    }

    /// <summary>선택지 액션. A1 = GoTo/EndDialogue. AcceptQuest/ClaimQuest 는 Phase B 에서 동작.</summary>
    public enum DialogueActionKind
    {
        GoTo,
        EndDialogue,
        AcceptQuest,
        ClaimQuest,
    }

    /// <summary>선택지 노출 조건. A1 = Always. 나머지는 Phase B(퀘스트 상태 기반).</summary>
    public enum DialogueShowCondition
    {
        Always,
        QuestNotAccepted,
        QuestInProgress,
        QuestCompleted,
        QuestClaimed,
    }
}
