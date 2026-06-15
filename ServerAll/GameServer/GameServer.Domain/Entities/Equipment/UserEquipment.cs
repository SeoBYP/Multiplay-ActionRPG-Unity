using Shared.Gameplay.Equipment;

namespace GameServer.Domain.Entities.Equipment;

/// <summary>
/// 한 유저의 *착용 상태*(영속 엔티티). (UserId, Slot) 복합키 → ItemId.
/// 한 슬롯에 하나만 착용(교체형). 정의(슬롯·스탯)는 EquipmentCatalog 가 소유 — 이 엔티티는 "무엇을 어디에 꼈는지"만.
/// 키 = user_id(미래 character_id 이관, Inventory/Progression 과 동일). [[character-swap-direction]]
/// </summary>
public class UserEquipment
{
    public long UserId { get; private set; }

    public EquipmentType Slot { get; private set; }

    public string ItemId { get; private set; } = string.Empty;

    public DateTime UpdatedAt { get; private set; }

    private UserEquipment() { }

    public static UserEquipment Create(long userId, EquipmentType slot, string itemId)
    {
        if (userId <= 0)
            throw new ArgumentException("UserId must be positive", nameof(userId));
        if (string.IsNullOrWhiteSpace(itemId))
            throw new ArgumentException("ItemId is required", nameof(itemId));

        return new UserEquipment
        {
            UserId = userId,
            Slot = slot,
            ItemId = itemId,
            UpdatedAt = DateTime.UtcNow,
        };
    }

    /// <summary>같은 슬롯의 착용 아이템을 교체한다(upsert 의 update 경로).</summary>
    public void ChangeItem(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            throw new ArgumentException("ItemId is required", nameof(itemId));

        ItemId = itemId;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>캐시(Redis Hash: slot→itemId)에서 복원. UpdatedAt 은 캐시에 없으므로 의미 없음.</summary>
    public static UserEquipment FromRedis(long userId, EquipmentType slot, string itemId)
        => new()
        {
            UserId = userId,
            Slot = slot,
            ItemId = itemId,
            UpdatedAt = DateTime.UtcNow,
        };
}
