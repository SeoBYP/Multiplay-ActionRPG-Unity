namespace GameServer.Domain.Entities.Codex;

/// <summary>
/// 도감 발견 기록(영속 엔티티). (UserId, ItemId) 복합키 → 한 번이라도 *획득*한 아이템.
/// 행이 존재하면 "발견함"(append-only). 수량·소유 여부와 무관 — 소유는 InventoryItem.
///
/// 키 = user_id (지금). 미래 캐릭터 교체 시 character_id 로 이관(Inventory·Progression 과 동일). [[character-swap-direction]]
/// 정의(이름·등급·아이콘)는 ItemCatalog(서버)·ItemDisplayCatalog(클라)가 소유 — 이 엔티티는 발견 사실만 기록.
/// </summary>
public class UserCodexEntry
{
    public long UserId { get; private set; }

    public int ItemId { get; private set; }

    /// <summary>최초 발견(획득) 시각.</summary>
    public DateTime DiscoveredAt { get; private set; }

    private UserCodexEntry() { }

    public static UserCodexEntry Create(long userId, int itemId)
    {
        if (userId <= 0)
            throw new ArgumentException("UserId must be positive", nameof(userId));
        if (itemId <= 0)
            throw new ArgumentException("ItemId must be positive", nameof(itemId));

        return new UserCodexEntry
        {
            UserId = userId,
            ItemId = itemId,
            DiscoveredAt = DateTime.UtcNow,
        };
    }
}
