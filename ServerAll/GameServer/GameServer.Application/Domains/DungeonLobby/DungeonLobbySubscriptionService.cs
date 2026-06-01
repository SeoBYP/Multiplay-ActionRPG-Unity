using System.Collections.Concurrent;
using GameServer.Application.Domains.DungeonLobby.Interfaces;
using Microsoft.Extensions.Logging;

namespace GameServer.Application.Domains.DungeonLobby;

public class DungeonLobbySubscriptionService(
    IDungeonRoomEventStream dungeonRoomEventStream,
    ILogger<DungeonLobbySubscriptionService> logger) : IDungeonLobbySubscriptionService
{
    private readonly ConcurrentDictionary<long, UserRoomContext> _contexts = new();

    public Task<UserRoomContext> SubscribeAsync(long userId, long roomId, CancellationToken ct)
    {
        try
        {
            if (_contexts.TryRemove(userId, out var existing))
            {
                existing.Stop();
            }

            var ctx = new UserRoomContext(userId, roomId);
            _contexts[userId] = ctx;
            
            _ = Task.Run(() => ReadLoopAsync(ctx, ctx.Cts.Token), CancellationToken.None);

            return Task.FromResult(ctx);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to subscribe to room {RoomId} for user {UserId}", roomId, userId);
            throw;
        }
    }

    public async Task PublishAsync(long roomId, CancellationToken ct)
    {
        try
        {
            logger.LogInformation("[SubscriptionService] PublishAsync room={RoomId}", roomId);
            await dungeonRoomEventStream.PublishAsync(roomId, ct);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to publish room update for room {RoomId}", roomId);
            throw;
        }
    }

    private async Task ReadLoopAsync(UserRoomContext ctx, CancellationToken ct)
    {
        logger.LogInformation("[ReadLoop] user={UserId} room={RoomId} 시작 (0-0부터 읽기)", ctx.UserId, ctx.RoomId);
        try
        {
            await foreach (var msg in dungeonRoomEventStream.ReadAsync(ctx.RoomId, "0-0", ct))
            {
                var written = ctx.Outbound.Writer.TryWrite(msg);
                logger.LogInformation("[ReadLoop] user={UserId} room={RoomId} 스트림 이벤트 수신 → Outbound TryWrite={Written}",
                    ctx.UserId, ctx.RoomId, written);
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("[ReadLoop] user={UserId} room={RoomId} 취소됨", ctx.UserId, ctx.RoomId);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Dungeon lobby read loop failed for user {UserId} room {RoomId}", ctx.UserId, ctx.RoomId);
            throw;
        }
        logger.LogInformation("[ReadLoop] user={UserId} room={RoomId} 종료", ctx.UserId, ctx.RoomId);
    }

    public async Task UnsubscribeAsync(UserRoomContext ctx, CancellationToken ct = default)
    {
        try
        {
            ctx.Stop();
            _contexts.TryRemove(ctx.UserId, out _);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to unsubscribe user {UserId} from room {RoomId}", ctx.UserId, ctx.RoomId);
            throw;
        }
    }
}
