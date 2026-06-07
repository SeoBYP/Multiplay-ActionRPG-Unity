namespace GameServer.Domain.Entities.Inventory;

/// <summary>
/// 아이템 *정의*(정적 기획데이터). 소유(InventoryItem)와 분리 — 정의는 DB가 아니라 코드 카탈로그.
/// </summary>
/// <param name="ItemId">카탈로그 키(문자열, 예 "potion_hp_small"). InventoryItem·proto가 참조하는 식별자.</param>
/// <param name="Name">표시 이름.</param>
/// <param name="Grade">등급(레어도).</param>
/// <param name="Stackable">스택 가능 여부. false면 MaxStack=1 취급(개별 인스턴스는 3.2 Equipment).</param>
/// <param name="MaxStack">한 칸 최대 수량.</param>
/// <param name="IconKey">클라 아이콘 룩업 키(표시 전용).</param>
public sealed record ItemDef(
    string ItemId,
    string Name,
    ItemGrade Grade,
    bool Stackable,
    int MaxStack,
    string IconKey);
