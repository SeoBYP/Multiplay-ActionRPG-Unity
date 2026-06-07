namespace GameServer.Application.Domains.Inventory;

/// <summary>
/// 아이템 소비(차감) 결과. 소모품/포션(3.8)·제작 소모 등이 받는 응답.
/// 보유 검증/차감은 서버 권위 — 클라는 "사용" 의도만 보낸다(클라 RPC는 3.8).
/// </summary>
public sealed record ItemConsumeResult(string ItemId, int RemainingQuantity, bool Success, string? FailReason = null)
{
    public static ItemConsumeResult Ok(string itemId, int remainingQuantity) => new(itemId, remainingQuantity, true);

    public static ItemConsumeResult Fail(string itemId, string reason) => new(itemId, 0, false, reason);
}
