using Server;
using Shared.Infrastructure.Messages;

namespace Server.Tests.Fakes;

/// <summary>
/// 플레이어 퇴장 이벤트 발행을 검증하기 위한 테스트 더블.
/// Redis를 거치지 않고 발행된 메시지를 메모리에 기록한다.
/// </summary>
public sealed class FakeRoomLifecyclePublisher : IRoomLifecyclePublisher
{
    public List<PlayerLeftRoomMessage> Published { get; } = new();

    public Task EnqueueAsync(PlayerLeftRoomMessage message)
    {
        Published.Add(message);
        return Task.CompletedTask;
    }
}
