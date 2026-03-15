using System.Collections.Concurrent;
using GameServer.Application.Domains.DungeonLobby.Interfaces;
using GameServer.Application.Domains.User.Interfaces;

namespace GameServer.Application.Domains.DungeonLobby;

public class DungeonLobbySubscriptionService(
    IDungeonRoomEventStream dungeonRoomEventStream,
    IDungeonRoomRepository roomRepository,
    IUserSessionRepository sessionRepository) : IDungeonLobbySubscriptionService
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

            if(session.CurrentRoomId != roomId || room.IsExist(session.UserId) == false)
                return null;
            
            var ctx = new UserRoomContext(session.UserId, roomId);

            // 기존 구독 삭제
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
            Console.WriteLine(e);
            throw;
        }
    }

    public async Task PublishAsync(long roomId, CancellationToken ct)
    {
        try
        {
            await dungeonRoomEventStream.PublishAsync(roomId,ct);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    private async Task ReadLoopAsync(UserRoomContext ctx, CancellationToken ct)
    {
        try
        {
            await foreach(var msg in dungeonRoomEventStream.ReadAsync(ctx.RoomId, "0-0", ct)) 
                ctx.Outbound.Writer.TryWrite(msg);
        }
        catch (OperationCanceledException) { /* 정상 종료, 무시 */ }
        catch (Exception e)
        {
            Console.WriteLine(e);
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
            Console.WriteLine(e);
            throw;
        }
    }
}