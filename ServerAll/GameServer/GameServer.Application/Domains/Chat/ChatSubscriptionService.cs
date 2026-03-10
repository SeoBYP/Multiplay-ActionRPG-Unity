using System.Collections.Concurrent;
using GameServer.Application.Domains.Chat.Interfaces;
using GameServer.Application.Domains.DungeonLobby.Interfaces;
using GameServer.Application.Domains.User.Interfaces;
using StackExchange.Redis;

namespace GameServer.Application.Domains.Chat;

public sealed class ChatSubscriptionService(
    IConnectionMultiplexer redis,
    IUserSessionRepository sessionRepository,
    IDungeonRoomRepository roomRepository) : IChatSubscriptionService
{
    private readonly ConcurrentDictionary<long, UserChatContext> _contexts = new();
    
    public async Task<UserChatContext?> ConnectAsync(string sessionId, CancellationToken ct)
    {
        var session = await sessionRepository.GetBySessionIdAsync(sessionId, ct);
        if (session is null) return null;

        var sub = redis.GetSubscriber();
        var ctx = new UserChatContext(session.UserId, session.NickName, session.CurrentRoomId, sub);

        // TODO : Redis Pub/Sub을 사용하면 Network 혹은 서버 장애시 메세지 유실에 대한 처리 필요,Pub/Sub을 MessageQueue로 변경 필요
        // Redis List를 활용해서 Queue로 활용, 또는 Kafka와 같은 Message Queue를 사용, 혹시 OutBox로 수정
        // => 결론은 메세지 유실에 대한 처리가 필요
        if (_contexts.TryGetValue(session.UserId, out var existing))
        {
            await DisconnectAsync(existing, ct);
        }
        _contexts[session.UserId] = ctx;

        await SubscribeIfNeeded(ctx, ChatChannels.GlobalChannel);
        await SubscribeIfNeeded(ctx, ChatChannels.WhisperChannel(ctx.Nickname));
        if (ctx.CurrentRoomId != 0)
            await SubscribeIfNeeded(ctx, ChatChannels.RoomChannel(ctx.CurrentRoomId));

        return ctx;
    }

    public async Task SwitchRoomAsync(string sessionId, long roomId, CancellationToken ct)
    {
        var session = await sessionRepository.GetBySessionIdAsync(sessionId, ct);
        if (session is null) return;

        // 연결중인 유저가 아니면(채팅 스트림 연결이 없으면) 구독 바꿀 필요 없음
        if (!_contexts.TryGetValue(session.UserId, out var ctx))
            return;

        // roomId 검증 (0은 leave)
        if (roomId != 0)
        {
            var room = await roomRepository.GetByIdAsync(roomId, ct);
            if (room is null) return;
            if (!room.IsExist(ctx.UserId)) return;
        }

        // 기존 방 구독 해제
        if (ctx.CurrentRoomId != 0)
            await UnsubscribeIfNeeded(ctx, ChatChannels.RoomChannel(ctx.CurrentRoomId));

        ctx.CurrentRoomId = roomId;

        // 새 방 구독
        if (ctx.CurrentRoomId != 0)
            await SubscribeIfNeeded(ctx, ChatChannels.RoomChannel(ctx.CurrentRoomId));
    }
    
    public async Task DisconnectAsync(UserChatContext ctx, CancellationToken ct = default)
    {
        ctx.Stop();

        foreach (var ch in ctx.SubscribedChannels)
        {
            try { await ctx.Subscriber.UnsubscribeAsync(ch, ctx.OnRedisMessage); }
            catch { }
        }
        ctx.SubscribedChannels.Clear();

        _contexts.TryRemove(ctx.UserId, out _);
    }

    private static async Task SubscribeIfNeeded(UserChatContext ctx, string channel)
    {
        RedisChannel ch = channel;
        if (ctx.SubscribedChannels.Add(ch))
            await ctx.Subscriber.SubscribeAsync(ch, ctx.OnRedisMessage);
    }

    private static async Task UnsubscribeIfNeeded(UserChatContext ctx, string channel)
    {
        RedisChannel ch = channel;
        if (ctx.SubscribedChannels.Remove(ch))
            await ctx.Subscriber.UnsubscribeAsync(ch, ctx.OnRedisMessage);
    }
}