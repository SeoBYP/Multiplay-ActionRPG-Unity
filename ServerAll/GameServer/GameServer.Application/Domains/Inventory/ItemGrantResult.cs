namespace GameServer.Application.Domains.Inventory;

/// <summary>
/// 아이템 지급 결과. 보상/루트(3.3)·상점(3.5)이 인벤토리 적립 후 받는 응답.
/// 멱등(중복지급 차단)은 호출자 책임 — Exp 보상(DungeonResultConsumer)과 동일 패턴.
/// </summary>
public sealed record ItemGrantResult(string ItemId, int NewQuantity, bool Success, string? FailReason = null)
{
    public static ItemGrantResult Ok(string itemId, int newQuantity) => new(itemId, newQuantity, true);

    public static ItemGrantResult Fail(string itemId, string reason) => new(itemId, 0, false, reason);
}
