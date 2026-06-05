using Shared.Infrastructure.Messages;

namespace Server;

/// <summary>
/// 던전 결과(클리어) 이벤트를 GameServer에 발행하는 계약.
/// RoomManager가 Redis 구체 구현(DungeonResultMessageQueue)에 직접 의존하지 않도록 분리한다.
/// 테스트에서는 발행 호출만 기록하는 Fake로 대체한다.
/// </summary>
public interface IDungeonResultPublisher
{
    Task EnqueueAsync(DungeonClearMessage message);
}
