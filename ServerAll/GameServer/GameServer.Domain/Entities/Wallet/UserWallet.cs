namespace GameServer.Domain.Entities.Wallet;

/// <summary>
/// 유저 재화(골드) 잔액(영속 엔티티). UserId 단일키 → 단일 잔액(스택형 아이템 아님).
///
/// 키 = user_id (지금). 미래 캐릭터 교체 시 character_id 로 이관(Progression·Inventory 와 동일). [[character-swap-direction]]
/// 골드는 통화 — 인벤토리 아이템(InventoryItem)이 아니라 별도 잔액으로 관리(3.4). 드랍/킬 보상·상점이 증감.
/// </summary>
public class UserWallet
{
    public long UserId { get; private set; }

    /// <summary>골드 잔액. 음수 불가. long(대량 누적 대비).</summary>
    public long Balance { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    private UserWallet() { }

    public static UserWallet Create(long userId, long balance = 0)
    {
        if (userId <= 0)
            throw new ArgumentException("UserId must be positive", nameof(userId));
        if (balance < 0)
            throw new ArgumentException("Balance cannot be negative", nameof(balance));

        return new UserWallet
        {
            UserId = userId,
            Balance = balance,
            UpdatedAt = DateTime.UtcNow,
        };
    }

    /// <summary>캐시(Redis String)에서 복원. UpdatedAt 은 캐시에 없으므로 의미 없음(표시 미사용).</summary>
    public static UserWallet FromRedis(long userId, long balance)
        => new()
        {
            UserId = userId,
            Balance = balance,
            UpdatedAt = DateTime.UtcNow,
        };

    /// <summary>잔액 증가. 0 이하는 무시.</summary>
    public void Add(long amount)
    {
        if (amount <= 0)
            return;

        Balance += amount;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>잔액 차감. 잔액보다 많거나 0 이하면 false(미차감).</summary>
    public bool TrySpend(long amount)
    {
        if (amount <= 0 || amount > Balance)
            return false;

        Balance -= amount;
        UpdatedAt = DateTime.UtcNow;
        return true;
    }
}
