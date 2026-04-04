using System.Collections.Concurrent;
using GameServer.Application.Domains.DungeonLobby.Interfaces;
using GameServer.Application.Domains.User.Interfaces;
using Microsoft.Extensions.Logging;

namespace GameServer.Application.Domains.DungeonLobby;

public class DungeonLobbySubscriptionService(
    IDungeonRoomEventStream dungeonRoomEventStream,
    IDungeonRoomRepository roomRepository,
    IDungeonRoomPlayerRepository roomPlayerRepository,
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

            // TODO : 구독 서비스가 비즈니스 로직을 하고 있다.
            //      추후 MSA를 구현할 때는 이 부분을 분리해야 한다.
            //      구독에 대한 책임만 처리한다.
            //      roomRepository를 들고 있는 게 아니라 Service로 처리해야 한다.
            var room = await roomRepository.GetByIdAsync(roomId, ct);
            if (room is null) return null;

            var roomPlayer = await roomPlayerRepository.GetByUserIdAsync(session.UserId, ct);
            if (roomPlayer is null || roomPlayer.RoomId != roomId)
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
