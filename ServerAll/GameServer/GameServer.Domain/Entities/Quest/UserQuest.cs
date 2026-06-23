namespace GameServer.Domain.Entities.Quest;

/// <summary>
/// 퀘스트 *수주/진행*(영속 엔티티). (UserId, QuestId) 복합키. 행 존재 = 수주함(없으면 미수주).
///
/// 키 = user_id (지금). 미래 캐릭터 교체 시 character_id 로 이관(다른 도메인과 동일). [[character-swap-direction]]
/// 완료 여부(Progress≥Required)는 RequiredCount(카탈로그) 기준이라 서비스가 판정 — 엔티티는 Status·Progress 만 관리.
/// </summary>
public class UserQuest
{
    public long UserId { get; private set; }

    public string QuestId { get; private set; } = string.Empty;

    public QuestStatus Status { get; private set; }

    public int Progress { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    private UserQuest() { }

    public static UserQuest Create(long userId, string questId)
    {
        if (userId <= 0)
            throw new ArgumentException("UserId must be positive", nameof(userId));
        if (string.IsNullOrWhiteSpace(questId))
            throw new ArgumentException("QuestId is required", nameof(questId));

        return new UserQuest
        {
            UserId = userId,
            QuestId = questId,
            Status = QuestStatus.Accepted,
            Progress = 0,
            UpdatedAt = DateTime.UtcNow,
        };
    }

    /// <summary>진행 누적. Accepted 상태에서만, required 상한까지. 실제 증가분이 있으면 true.</summary>
    public bool AddProgress(int amount, int required)
    {
        if (Status != QuestStatus.Accepted || amount <= 0 || Progress >= required)
            return false;

        var next = Math.Min(Progress + amount, required);
        if (next == Progress)
            return false;

        Progress = next;
        UpdatedAt = DateTime.UtcNow;
        return true;
    }

    /// <summary>보상 수령 마킹. Accepted + 완료(progress≥required)일 때만. 성공 시 true(=지급 진행).</summary>
    public bool Claim(int required)
    {
        if (Status != QuestStatus.Accepted || Progress < required)
            return false;

        Status = QuestStatus.Claimed;
        UpdatedAt = DateTime.UtcNow;
        return true;
    }
}
