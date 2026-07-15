namespace GameServer.Domain.Entities.Quest;

/// <summary>
/// 퀘스트 정의 카탈로그 — 코드 시드(정적 기획데이터). DB 테이블 아님(ItemCatalog·ShopCatalog 동형).
/// 수주/진행 상태만 DB(UserQuest)에 영속한다.
///
/// MVP: Main 에서 잡히는 monster=creepy_demon 뿐이라 KillMonster 는 creepy_demon 대상만 실제 진행한다.
/// quest_potion_collect 는 목표타입 구조 시연(CollectItem) — 진행 훅은 후속(GrantItemAsync 합류 시).
/// </summary>
public static class QuestCatalog
{
    // questId 는 유지(UserQuest 영속·수주 이력 호환). 목표 몬스터만 slime→creepy_demon 으로 교체.
    private static readonly Dictionary<string, QuestDef> Quests = new()
    {
        ["quest_slime_hunt"] = new QuestDef(
            "quest_slime_hunt", "데몬 사냥", "들판의 데몬 3마리를 처치하라.",
            QuestObjectiveType.KillMonster, "creepy_demon", 3,
            new QuestReward(Exp: 50, Gold: 100, ItemId: null, ItemQty: 0)),

        ["quest_slime_slayer"] = new QuestDef(
            "quest_slime_slayer", "데몬 토벌대", "데몬 5마리를 더 처치해 토벌대에 합류하라.",
            QuestObjectiveType.KillMonster, "creepy_demon", 5,
            new QuestReward(Exp: 80, Gold: 0, ItemId: "potion_hp_small", ItemQty: 2)),

        ["quest_potion_collect"] = new QuestDef(
            "quest_potion_collect", "물약 수집", "소형 체력 물약 3개를 모아라.",
            QuestObjectiveType.CollectItem, "potion_hp_small", 3,
            new QuestReward(Exp: 30, Gold: 50, ItemId: null, ItemQty: 0)),

        // TalkToNpc(4.5 Phase C) — 대상 NPC 와 1회 대화하면 완료. TargetId = npcId(DialogueCatalog 키와 동일).
        // RequiredCount=1 + 진행 상한으로 반복 대화 멱등(파밍 불가).
        ["quest_greet_elder"] = new QuestDef(
            "quest_greet_elder", "마을 어르신께 인사", "마을 어르신(npc_elder)과 대화하라.",
            QuestObjectiveType.TalkToNpc, "npc_elder", 1,
            new QuestReward(Exp: 30, Gold: 50, ItemId: null, ItemQty: 0)),
    };

    /// <summary>정의가 존재하는 questId 인지.</summary>
    public static bool Contains(string questId) => Quests.ContainsKey(questId);

    /// <summary>정의를 반환. 없으면 null.</summary>
    public static QuestDef? Get(string questId) => Quests.GetValueOrDefault(questId);

    /// <summary>전체 정의(조회/병합용).</summary>
    public static IReadOnlyCollection<QuestDef> All => Quests.Values;
}
