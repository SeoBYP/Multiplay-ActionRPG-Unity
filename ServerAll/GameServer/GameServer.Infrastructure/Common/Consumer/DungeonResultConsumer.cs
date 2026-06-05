using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared.Infrastructure.MessageQueue;
using Shared.Infrastructure.Messages;

namespace GameServer.Infrastructure.Common.Consumer;

/// <summary>
/// SocketServer가 발행한 던전 클리어 이벤트를 소비한다.
///
/// M4 A 트랙: 수신·로그까지만(루프 골격 검증). 보상 산정/지급(경험치+아이템)은 B 트랙에서
/// Inventory/Progression 도메인이 합류하며 이 ProcessAsync 안에서 Outbox 원자화로 처리한다.
/// </summary>
public sealed class DungeonResultConsumer(
    IMessageQueue<DungeonClearMessage> dungeonClearQueue,
    ILogger<DungeonResultConsumer> logger) : BackgroundService
{
    // 복원력(일시적 Redis 오류에 죽지 않음)은 ResilientStreamConsumer 공통 루프가 담당.
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
        => ResilientStreamConsumer.RunAsync<DungeonClearMessage>(
            nameof(DungeonResultConsumer),
            dungeonClearQueue.DequeueAllAsync,
            ProcessAsync,
            logger,
            stoppingToken);

    private Task ProcessAsync(DungeonClearMessage message, CancellationToken ct)
    {
        // TODO(B 트랙): 참가자별 보상 산정(경험치/아이템) → Progression.AddExp + Inventory.Grant (Outbox 원자화).
        logger.LogInformation(
            "[DungeonResult] Clear received: RoomId={RoomId} MapId={MapId} Participants=[{Participants}]",
            message.RoomId, message.MapId, string.Join(",", message.Participants));
        return Task.CompletedTask;
    }
}
