using System.Collections.Concurrent;
using GameServer.Application.Services.DungeonLobby.Interfaces;
using GameServer.Infrastructure.Interfaces.DungeonRoom;
using GameServer.Infrastructure.Interfaces.User;
using StackExchange.Redis;

namespace GameServer.Application.Services.DungeonLobby;

public class DungeonLobbySubscriptionService(
    IConnectionMultiplexer redis,
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
            
            var sub = redis.GetSubscriber();
            var ctx = new UserRoomContext(session.UserId, roomId, sub);

            if (_contexts.TryGetValue(session.UserId, out var existing))
            {
                // 이미 구독 중인 방이 있으면 Disconnect
                await UnsubscribeAsync(existing, ct);
            }

            var redisChannel = new RedisChannel(RoomChannels.RoomChannel(roomId), RedisChannel.PatternMode.Auto);
            await ctx.Subscriber.SubscribeAsync(redisChannel, ctx.OnRedisMessage);
            _contexts[session.UserId] = ctx;

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
            var redisChannel = new RedisChannel(RoomChannels.RoomChannel(roomId), RedisChannel.PatternMode.Auto);
            await redis.GetSubscriber().PublishAsync(redisChannel, roomId);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    public async Task UnsubscribeAsync(UserRoomContext ctx, CancellationToken ct = default)
    {
        if (ctx is null)
            return;
        try
        {
            ctx.Stop();
            await ctx.Subscriber.UnsubscribeAllAsync();
            _contexts.TryRemove(ctx.UserId, out _);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }
}