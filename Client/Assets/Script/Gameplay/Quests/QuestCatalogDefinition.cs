using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Gameplay.Quests
{
    /// <summary>
    /// 퀘스트 목표 종류(저작용). 서버 <c>Shared.Infrastructure.Quests.QuestObjectiveType</c> 의 미러 —
    /// 그쪽은 서버 전용 어셈블리라 클라가 참조할 수 없다(MonsterTierId 와 동일 선례).
    /// <b>계약은 JSON 의 문자열</b>("KillMonster"/"CollectItem"/"TalkToNpc")이고 proto enum 과 정수값 1:1.
    /// </summary>
    public enum QuestObjectiveId
    {
        KillMonster = 0,
        CollectItem = 1,
        TalkToNpc = 2,
    }

    /// <summary>
    /// 퀘스트 정의 저작(authoring) 진실원. 디자이너가 이 SO 하나를 Inspector 에서 편집한다.
    /// (ItemCatalogDefinition·MonsterCatalogDefinition 과 동일 컨벤션 — 단일 SO + List, SO 저작 → JSON bake → 서버 임베디드.)
    ///
    /// <para><b>왜 뒤늦게 생겼나</b>: bake 산출물 7종 중 <c>items.json</c> 과 <c>quests.json</c> 만 Exporter 가 없어
    /// 손으로 저작해 왔다. items 는 실제로 클라 카탈로그와 갈라졌고(A4), quests 도 같은 위험을 안고 있었다.
    /// 이 SO 가 마지막 수기 저작을 없앤다 — <b>이제 7종 전부 SO→bake 로 강제된다</b>.</para>
    ///
    /// <para><b>Name·Description 은 bake 한다</b>: 아이템 표시 필드와 달리 서버가 proto(QuestInfo)로 클라에
    /// 직접 보내므로 서버가 알아야 한다.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "QuestCatalogDefinition", menuName = "Game/Quest Catalog Definition", order = 6)]
    public sealed class QuestCatalogDefinition : ScriptableObject
    {
        [Tooltip("퀘스트 정의 목록. questId 는 서버·클라 공용 키다.")]
        public List<QuestDefinition> quests = new();

        /// <summary>questId 의 정의. 미등록이면 null.</summary>
        public QuestDefinition Get(string questId)
        {
            foreach (var q in quests)
                if (q.questId == questId)
                    return q;
            return null;
        }
    }

    /// <summary>퀘스트 1종 — 목표(무엇을 몇 번) + 완료 보상(exp/gold/item 조합).</summary>
    [Serializable]
    public sealed class QuestDefinition
    {
        [Tooltip("퀘스트 키(서버·클라 공용). 예: quest_slime_hunt")]
        public string questId;

        [Header("표시 (서버가 proto 로 클라에 보낸다)")]
        public string displayName;
        [TextArea(2, 4)] public string description;

        [Header("목표")]
        public QuestObjectiveId objectiveType = QuestObjectiveId.KillMonster;

        [Tooltip("목표 대상. KillMonster=monsterId · TalkToNpc=npcId · CollectItem=itemId(문자열).\n" +
                 "⚠ CollectItem 은 진행 훅이 아직 없다(GrantItemAsync 합류 시 후속). 그때 아이템 키를 " +
                 "numericId(int)로 옮길지 함께 정한다 — 지금은 monsterId·npcId 와 한 필드를 쓰느라 문자열이다.")]
        public string targetId;

        [Tooltip("완료에 필요한 수.")]
        public int requiredCount = 1;

        [Header("보상 (0 이면 해당 보상 없음)")]
        public long rewardExp;
        public long rewardGold;

        [Tooltip("지급 아이템의 numericId(ItemCatalogDefinition). 0 이면 아이템 보상 없음.\n" +
                 "대역: 1000 소모품 / 2100 무기 / 2200 방어구 / 2300 장신구 / 3000 재화.")]
        public int rewardItemId;

        public int rewardItemQty;
    }
}
