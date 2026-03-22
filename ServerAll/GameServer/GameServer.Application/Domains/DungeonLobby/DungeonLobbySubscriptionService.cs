using System.Collections.Concurrent;
using GameServer.Application.Domains.DungeonLobby.Interfaces;
using GameServer.Application.Domains.User.Interfaces;
using Microsoft.Extensions.Logging;

namespace GameServer.Application.Domains.DungeonLobby;

public class DungeonLobbySubscriptionService(
    IDungeonRoomEventStream dungeonRoomEventStream,
    IDungeonRoomRepository roomRepository,
    IUserSessionRepository sessionRepository,
    ILogger<DungeonLobbySubscriptionService> logger) : IDungeonLobbySubscriptionService
{
    private readonly ConcurrentDictionary<long, UserRoomContext> _contexts = new();

    public async Task<UserRoomContext?> SubscribeAsync(string sessionId, long roomId, CancellationToken ct)
    {
        try
        {
            var session = await sessionRepository.GetBySessionIdAsync(sessionId, ct);
            if (session is null) return null;

            var room = await roomRepository.GetByIdAsync(roomId, ct);
            if (room is null) return null;

            if (session.CurrentRoomId != roomId || room.IsExist(session.UserId) == false)
                return null;

            var ctx = new UserRoomContext(session.UserId, roomId);

            if (_contexts.TryGetValue(session.UserId, out var existing))
            {
                existing.Stop();
            }

            _contexts[session.UserId] = ctx;
            _ = Task.Run(() => ReadLoopAsync(ctx, ctx.Cts.Token));

            return ctx;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to subscribe to room {RoomId}", roomId);
            throw;
        }
    }

    public async Task PublishAsync(long roomId, CancellationToken ct)
    {
        try
        {
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
        try
        {
            await foreach (var msg in dungeonRoomEventStream.ReadAsync(ctx.RoomId, "0-0", ct))
                ctx.Outbound.Writer.TryWrite(msg);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception e)
        {
            logger.LogError(e, "Dungeon lobby read loop failed for user {UserId} room {RoomId}", ctx.UserId, ctx.RoomId);
            throw;
        }
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
