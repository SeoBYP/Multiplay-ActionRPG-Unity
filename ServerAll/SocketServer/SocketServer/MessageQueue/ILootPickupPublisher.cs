using Shared.Infrastructure.Messages;

namespace Server;

/// <summary>
/// 줍기 확정(바닥 아이템 픽업) 을 GameServer 에 발행하는 계약.
/// RoomManager 가 Redis 구체 구현(LootPickupMessageQueue)에 직접 의존하지 않도록 분리한다.
/// 테스트에서는 발행 호출만 기록하는 Fake 로 대체한다(IDungeonResultPublisher 와 동일 패턴).
/// </summary>
public interface ILootPickupPublisher
{
    Task EnqueueAsync(ItemPickedUpMessage message);
}
