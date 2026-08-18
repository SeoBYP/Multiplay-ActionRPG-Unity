namespace Shared.Infrastructure.Messages;

/// <summary>
/// SocketServer → GameServer: 플레이어가 바닥 아이템 줍기에 성공하면 1회 발행(던전 경로).
///
/// GameServer 는 이 메시지를 소비해(LootGrantConsumer) 인벤토리에 영속 지급한다(GrantItemAsync → Create/Update).
/// 경계 넘는 데이터는 딱 이것만(loot-drop.md §1.1): 월드(SocketServer)는 itemId 문자열만 알고,
/// 정의 검증(ItemCatalog)·영속은 GameServer 가 소유한다.
///
/// PickupId 는 멱등 키(at-most-once) — GameServer 가 Redis SET claim 으로 중복 지급을 차단한다.
/// 줍기 1회 보장은 SocketServer(경쟁 중재)가, 메시지 재전달 중복 방어는 GameServer 가 담당.
/// </summary>
public sealed class ItemPickedUpMessage
{
    public long UserId { get; init; }
    public int ItemId { get; init; }   // numericId. 대역 3000~3999 = 재화(지갑 라우팅)
    public int Qty { get; init; }
    public string PickupId { get; init; } = "";
    public string TraceId { get; init; } = "";
}
