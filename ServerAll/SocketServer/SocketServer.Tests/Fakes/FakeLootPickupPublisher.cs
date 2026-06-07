using Server;
using Shared.Infrastructure.Messages;

namespace Server.Tests.Fakes;

/// <summary>
/// 줍기 확정 발행을 검증하기 위한 테스트 더블.
/// Redis를 거치지 않고 발행된 메시지를 메모리에 기록한다(FakeDungeonResultPublisher 와 동일 패턴).
/// </summary>
public sealed class FakeLootPickupPublisher : ILootPickupPublisher
{
    public List<ItemPickedUpMessage> Published { get; } = new();

    public Task EnqueueAsync(ItemPickedUpMessage message)
    {
        Published.Add(message);
        return Task.CompletedTask;
    }
}
