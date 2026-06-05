using GameServer.Application.Domains.DungeonLobby.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared.Infrastructure.MessageQueue;
using Shared.Infrastructure.Messages;

namespace GameServer.Infrastructure.Common.Consumer;

/// <summary>
/// SocketServer가 발행한 PlayerLeft 이벤트를 소비해 GameServer에서 해당 플레이어의
/// 방 association을 정리한다(빈 방이면 방 삭제, 남으면 호스트 이양).
/// </summary>
public sealed class RoomLifecycleConsumer(
    IMessageQueue<PlayerLeftRoomMessage> playerLeftQueue,
    IServiceScopeFactory scopeFactory,
    ILogger<RoomLifecycleConsumer> logger) : BackgroundService
{
    // 복원력(일시적 Redis 오류에 죽지 않음)은 ResilientStreamConsumer 공통 루프가 담당.
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
        => ResilientStreamConsumer.RunAsync<PlayerLeftRoomMessage>(
            nameof(RoomLifecycleConsumer),
            playerLeftQueue.DequeueAllAsync,
            ProcessAsync,
            logger,
            stoppingToken);

    private async Task ProcessAsync(PlayerLeftRoomMessage message, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var lobbyService = scope.ServiceProvider.GetRequiredService<IDungeonLobbyService>();
        var result = await lobbyService.RemovePlayerFromRoomAsync(message.RoomId, message.UserId, ct);

        if (result.IsSuccess)
            logger.LogInformation("[RoomLifecycle] Player {UserId} removed from room {RoomId} (emptied={Emptied})", message.UserId, message.RoomId, message.RoomEmptied);
        else
            logger.LogWarning("[RoomLifecycle] Player {UserId} remove from room {RoomId} failed: {Message}", message.UserId, message.RoomId, result.Message);
    }
}
