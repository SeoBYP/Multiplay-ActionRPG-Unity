namespace GameServer.Application.Domains.Inventory;

/// <summary>
/// Main 킬 클레임(B-lite) 결과.
/// - 성공+보상: 슬롯 유효 + 쿨다운 경과 → 서버 roll 로 지급된 항목들.
/// - 성공+빈목록: 쿨다운 중(재청구 차단) 또는 drop 없음 — 에러 아님(보상만 없음).
/// - 실패: 위조 슬롯/맵 — 정상 플레이에선 발생 안 함(치팅/버그 신호).
/// </summary>
public sealed record MainClaimResult(bool Success, IReadOnlyList<GrantedItem> Granted, string? FailReason = null)
{
    public static MainClaimResult Ok(IReadOnlyList<GrantedItem> granted) => new(true, granted);

    /// <summary>쿨다운 중(재청구 차단) — 정상 결과, 보상만 없음.</summary>
    public static MainClaimResult OnCooldown() => new(true, Array.Empty<GrantedItem>());

    /// <summary>위조 슬롯/맵 등 — 검증 실패.</summary>
    public static MainClaimResult Fail(string reason) => new(false, Array.Empty<GrantedItem>(), reason);
}

/// <summary>ClaimKill 로 실제 지급된 한 항목.</summary>
public sealed record GrantedItem(int ItemId, int Qty, int NewQuantity);

/// <summary>
/// Main 킬 경험치 청구(킬 즉시) 결과. ExpGained = 적립된 exp(쿨다운 중이면 0, 에러 아님).
/// </summary>
public sealed record MainExpClaimResult(bool Success, long ExpGained, string? FailReason = null)
{
    public static MainExpClaimResult Ok(long expGained) => new(true, expGained);

    /// <summary>쿨다운 중(재청구 차단) — 정상 결과, exp 0.</summary>
    public static MainExpClaimResult OnCooldown() => new(true, 0);

    /// <summary>위조 슬롯/맵 등 — 검증 실패.</summary>
    public static MainExpClaimResult Fail(string reason) => new(false, 0, reason);
}
