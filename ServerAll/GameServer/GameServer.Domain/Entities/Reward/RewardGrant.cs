namespace GameServer.Domain.Entities.Reward;

/// <summary>
/// 보상 지급 원장 — "이 지급은 이미 했다"의 **단일 진실**.
///
/// 왜 DB 인가: 지급(Exp 적립·인벤토리 증가·지갑 적립)과 "지급했음" 기록이 **같은 트랜잭션**에 있어야
/// exactly-once 가 성립한다. Redis 키로 잠그면 키와 지급이 서로 다른 저장소라
/// "지급됐는데 기록이 없다" 또는 "기록됐는데 지급이 없다" 창이 항상 남는다.
/// (실제로 참가자별 개별 트랜잭션이라, 4인 파티 3번째에서 실패하면 1·2 는 이미 커밋돼 있다.)
///
/// <see cref="GrantKey"/> 하나에 UNIQUE 를 걸어 멱등 범위를 표현한다:
///   던전 Exp  = "dungeon:{roomId}:{userId}"   (참가자 1명 = 1건 → 부분 재시도가 안전)
///   줍기 지급 = "pickup:{pickupId}"           (줍기 1건 = 1건)
///
/// 부수 효과로 지급 이력이 남아 CS 대응·정산 추적이 가능해진다.
/// </summary>
public class RewardGrant
{
    public long GrantId { get; private set; }

    /// <summary>멱등 범위 전체를 담은 키. UNIQUE — 진짜 방어선은 이 인덱스다.</summary>
    public string GrantKey { get; private set; } = "";

    public long UserId { get; private set; }

    /// <summary>"exp" | "item" | "currency" — 조회·정산용 분류.</summary>
    public string Kind { get; private set; } = "";

    /// <summary>아이템 지급이면 itemId. 없으면 빈 문자열.</summary>
    public string RefId { get; private set; } = "";

    public long Amount { get; private set; }

    public DateTime GrantedAt { get; private set; }

    private RewardGrant() { }

    public static RewardGrant Create(string grantKey, long userId, string kind, string refId, long amount)
    {
        if (string.IsNullOrWhiteSpace(grantKey))
            throw new ArgumentException("GrantKey is required", nameof(grantKey));
        if (userId <= 0)
            throw new ArgumentException("UserId must be positive", nameof(userId));
        if (string.IsNullOrWhiteSpace(kind))
            throw new ArgumentException("Kind is required", nameof(kind));

        return new RewardGrant
        {
            GrantKey = grantKey,
            UserId = userId,
            Kind = kind,
            RefId = refId ?? "",
            Amount = amount,
            GrantedAt = DateTime.UtcNow,
        };
    }
}
