namespace GameServer.Domain.Entities.Inventory;

/// <summary>
/// 아이템 *소유*(영속 엔티티). (UserId, ItemId) 복합키 → Quantity 스택형.
/// ItemId = numericId(int). 대역이 곧 분류다 — 1000 소모품 / 2100 무기 / 2200 방어구 / 2300 장신구.
///
/// 키 = user_id (지금). 미래 캐릭터 교체 시 character_id 로 이관(Progression 과 동일). [[character-swap-direction]]
/// 정의(이름·등급·MaxStack)는 ItemCatalog 가 소유 — 이 엔티티는 수량만 관리(MaxStack clamp 는 서비스 책임).
/// </summary>
public class InventoryItem
{
    public long UserId { get; private set; }

    public int ItemId { get; private set; }

    public int Quantity { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    private InventoryItem() { }

    public static InventoryItem Create(long userId, int itemId, int quantity)
    {
        if (userId <= 0)
            throw new ArgumentException("UserId must be positive", nameof(userId));
        if (itemId <= 0)
            throw new ArgumentException("ItemId must be positive", nameof(itemId));
        if (quantity <= 0)
            throw new ArgumentException("Quantity must be positive", nameof(quantity));

        return new InventoryItem
        {
            UserId = userId,
            ItemId = itemId,
            Quantity = quantity,
            UpdatedAt = DateTime.UtcNow,
        };
    }

    /// <summary>캐시(Redis Hash: itemId→qty)에서 복원. UpdatedAt 은 캐시에 없으므로 의미 없음(표시 미사용).</summary>
    public static InventoryItem FromRedis(long userId, int itemId, int quantity)
        => new()
        {
            UserId = userId,
            ItemId = itemId,
            Quantity = quantity,
            UpdatedAt = DateTime.UtcNow,
        };

    /// <summary>수량 누적. 0 이하는 무시.</summary>
    public void Add(int amount)
    {
        if (amount <= 0)
            return;

        Quantity += amount;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>수량 차감. 보유보다 많거나 0 이하면 false(미차감).</summary>
    public bool Remove(int amount)
    {
        if (amount <= 0 || amount > Quantity)
            return false;

        Quantity -= amount;
        UpdatedAt = DateTime.UtcNow;
        return true;
    }
}
