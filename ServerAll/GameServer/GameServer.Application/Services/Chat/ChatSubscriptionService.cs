using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Channels;
using GameServer.Application.Services.Auth.Interfaces;
using GameServer.Application.Services.Chat.Interfaces;
using GameServer.Domain.Entities.Chat;
using GameServer.Infrastructure.Interfaces.User;
using StackExchange.Redis;

namespace GameServer.Application.Services.Chat;

public class ChatSubscriptionService(IConnectionMultiplexer redis,
    IUserSessionRepository sessionRepository) : IChatSubscriptionService
{
    public async IAsyncEnumerable<ChatMessage> SubscribeGlobalAsync(string sessionId,[EnumeratorCancellation] CancellationToken ct = default)
    {
        // 1. 인증 확인
        var session = await sessionRepository.GetBySessionIdAsync(sessionId);
        if (session is null) yield break; // 인증 실패 → 빈 스트림 반환

        // 2. 실제 구독
        await foreach (var msg in SubscribeChannelAsync(ChatChannels.GlobalChannel, ct))
        {
            yield return msg;
        }
    }

    
    public async IAsyncEnumerable<ChatMessage> SubscribeRoomAsync(string sessionId, long roomId,[EnumeratorCancellation] CancellationToken ct = default)
    {
        // 1. 인증 확인
        var session = await sessionRepository.GetBySessionIdAsync(sessionId);
        if (session is null) yield break;

        // 2. RoomId 검증
        if (roomId <= 0) yield break;

        // 3. 실제 구독
        await foreach (var msg in SubscribeChannelAsync(ChatChannels.RoomChannel(roomId), ct))
        {
            yield return msg;
        }
    }

    private async IAsyncEnumerable<ChatMessage> SubscribeChannelAsync(
        string channelName,
        [EnumeratorCancellation] CancellationToken ct)
    {
        // Channel<T>: 스레드 안전한 큐
        // Capacity 100: 버퍼 제한 (메모리 보호)
        var queue = Channel.CreateBounded<ChatMessage>(new BoundedChannelOptions(100)
        {
            FullMode = BoundedChannelFullMode.DropOldest // 큐 가득 차면 오래된 것 삭제
        });

        var subscriber = redis.GetSubscriber();

        // Redis 콜백: 메시지 도착 시 큐에 넣기
        // 주의: 이 콜백은 Redis 스레드에서 실행됨!
        void OnMessage(RedisChannel channel, RedisValue value)
        {
            if (value.IsNullOrEmpty) return;

            try
            {
                var chatData = value.ToString();
                var chatMessage = JsonSerializer.Deserialize<ChatMessage>(chatData);
                if (chatMessage != null)
                {
                    // TryWrite: 큐가 가득 차도 예외 발생 안 함
                    queue.Writer.TryWrite(chatMessage);
                }
            }
            catch
            {
                // 역직렬화 실패 무시 (잘못된 메시지)
            }
        }

        // Redis 구독 시작
        await subscriber.SubscribeAsync(channelName, OnMessage);

        try
        {
            // ct(CancellationToken)이 취소될 때까지 큐에서 메시지를 꺼내서 반환
            await foreach (var msg in queue.Reader.ReadAllAsync(ct))
            {
                yield return msg;
            }
        }
        finally
        {
            // 클라이언트 연결 끊김 or 취소 시 반드시 정리!
            // 정리 안 하면 Redis 메모리 누수 발생
            await subscriber.UnsubscribeAsync(channelName, OnMessage);
            queue.Writer.TryComplete();
        }
    }
}