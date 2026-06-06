namespace GameServer.Domain.Entities.User;

/// <summary>
/// 플레이어 진행(레벨·경험치) 영속 엔티티. users 와 1:1 (PK=FK), user_profiles 와 동일 패턴.
///
/// 미래 캐릭터 교체(원신식) 도입 시 진행은 캐릭터에 귀속된다 → 키를 character_id 로 이관.
/// 지금은 계정당 암묵적 캐릭터 1개라 user_id 를 키로 둔다.
/// Exp 만 우선 적립하며, 레벨업 산식은 후속(M5 스탯 산식)에서 채운다.
/// </summary>
public class UserProgression
{
    public const int InitialLevel = 1;

    /// <summary>UserId (DB PK = users FK)</summary>
    public long UserId { get; private set; }

    public int Level { get; private set; } = InitialLevel;

    public long Exp { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    private UserProgression() { }

    public static UserProgression Create(long userId)
    {
        if (userId <= 0)
            throw new ArgumentException("UserId must be positive", nameof(userId));

        return new UserProgression
        {
            UserId = userId,
            Level = InitialLevel,
            Exp = 0,
            UpdatedAt = DateTime.UtcNow,
        };
    }

    public static UserProgression FromRedis(long userId, int level, long exp, DateTime updatedAt)
        => new()
        {
            UserId = userId,
            Level = level,
            Exp = exp,
            UpdatedAt = updatedAt,
        };

    /// <summary>경험치를 누적 적립한다. 0 이하는 무시. 레벨업 산식은 후속(M5).</summary>
    public void AddExp(long amount)
    {
        if (amount <= 0)
            return;

        Exp += amount;
        UpdatedAt = DateTime.UtcNow;
    }
}
