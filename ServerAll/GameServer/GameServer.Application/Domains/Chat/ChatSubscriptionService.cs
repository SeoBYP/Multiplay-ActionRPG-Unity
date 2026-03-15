using System.Collections.Concurrent;
using GameServer.Application.Domains.Chat.Interfaces;
using GameServer.Application.Domains.DungeonLobby.Interfaces;
using GameServer.Application.Domains.User.Interfaces;

namespace GameServer.Application.Domains.Chat;

public sealed class ChatSubscriptionService(
    IChatStreamReader chatStreamReader,
    IUserSessionRepository sessionRepository,
    IDungeonRoomRepository roomRepository) : IChatSubscriptionService
{
    private readonly ConcurrentDictionary<long, UserChatContext> _contexts = new();
    
    public async Task<UserChatContext?> ConnectAsync(string sessionId, CancellationToken ct)
    {
        var session = await sessionRepository.GetBySessionIdAsync(sessionId, ct);
        if (session is null) return null;
        
        var ctx = new UserChatContext(session.UserId, session.NickName, session.CurrentRoomId);
        
        // TODO : Redis Pub/Sub을 사용하면 Network 혹은 서버 장애시 메세지 유실에 대한 처리 필요,Pub/Sub을 MessageQueue로 변경 필요
        // Redis List를 활용해서 Queue로 활용, 또는 Kafka와 같은 Message Queue를 사용, 혹시 OutBox로 수정
        // => 결론은 메세지 유실에 대한 처리가 필요
        if (_contexts.TryGetValue(session.UserId, out var existing))
        {
            await DisconnectAsync(existing, ct);
        }
        _contexts[session.UserId] = ctx;

        var list = new List<string> { ChatChannels.GlobalChannel, ChatChannels.WhisperChannel(ctx.Nickname) };
        if (ctx.CurrentRoomId != 0)
            list.Add(ChatChannels.RoomChannel(ctx.CurrentRoomId));

        _ = Task.Run(() => ReadLoopAsync(ctx, list, ctx.ReadLoopCts.Token));
        
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
        await ctx.ReadLoopCts.CancelAsync();               // ReadLoop만 종료
        ctx.ReadLoopCts = new CancellationTokenSource();  // 새 토큰
        
        ctx.CurrentRoomId = roomId;
        
        // 새 채널로 ReadLoop 재시작
        var newChannels = new List<string> { ChatChannels.GlobalChannel, ChatChannels.WhisperChannel(ctx.Nickname) };
        if (ctx.CurrentRoomId != 0)
            newChannels.Add(ChatChannels.RoomChannel(ctx.CurrentRoomId));

        _ = Task.Run(() => ReadLoopAsync(ctx, newChannels, ctx.ReadLoopCts.Token));

    }
    
    public Task DisconnectAsync(UserChatContext ctx, CancellationToken ct = default)
    {
        try
        {
            ctx.Stop();
            _contexts.TryRemove(ctx.UserId, out _);
            return Task.CompletedTask;
        }
        catch (Exception exception)
        {
            return Task.FromException(exception);
        }
    }

    private async Task ReadLoopAsync(UserChatContext ctx,
        IReadOnlyList<string> channels,
        CancellationToken ct)
    {
        try
        {
            await foreach (var msg in chatStreamReader.ReadAsync(channels, "0-0", ct))
                ctx.Outbound.Writer.TryWrite(msg);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
        finally
        {
            // 전체 연결이 끊어질 때만 Outbound 닫기
            if (ctx.Cts.IsCancellationRequested)
                ctx.Outbound.Writer.TryComplete();
        }
    }
}