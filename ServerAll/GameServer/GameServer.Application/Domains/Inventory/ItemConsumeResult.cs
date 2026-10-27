namespace GameServer.Application.Domains.Inventory;

/// <summary>
/// 아이템 소비(차감) 결과. 보유 검증/차감은 서버 권위 — 클라는 "사용" 의도만 보낸다.
/// </summary>
public sealed record ItemConsumeResult(int ItemId, int RemainingQuantity, bool Success, string? FailReason = null)
{
    public static ItemConsumeResult Ok(int itemId, int remainingQuantity) => new(itemId, remainingQuantity, true);

    public static ItemConsumeResult Fail(int itemId, string reason) => new(itemId, 0, false, reason);
}
