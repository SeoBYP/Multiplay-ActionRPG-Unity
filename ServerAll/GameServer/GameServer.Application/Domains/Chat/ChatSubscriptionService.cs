using System.Collections.Concurrent;
using GameServer.Application.Domains.Chat.Interfaces;
using GameServer.Application.Domains.DungeonLobby.Interfaces;
using GameServer.Application.Domains.User.Interfaces;
using Microsoft.Extensions.Logging;

namespace GameServer.Application.Domains.Chat;

public sealed class ChatSubscriptionService(
    IChatEventStream chatEventStream,
    IUserSessionRepository sessionRepository,
    IDungeonRoomRepository roomRepository,
    ILogger<ChatSubscriptionService> logger) : IChatSubscriptionService
{
    private readonly ConcurrentDictionary<long, UserChatContext> _contexts = new();

    public async Task<UserChatContext?> ConnectAsync(string sessionId, CancellationToken ct)
    {
        var session = await sessionRepository.GetBySessionIdAsync(sessionId, ct);
        if (session is null) return null;

        var ctx = new UserChatContext(session.UserId, session.NickName, session.CurrentRoomId);

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

        if (!_contexts.TryGetValue(session.UserId, out var ctx))
            return;

        if (roomId != 0)
        {
            var room = await roomRepository.GetByIdAsync(roomId, ct);
            if (room is null) return;
            if (!room.IsExist(ctx.UserId)) return;
        }

        await ctx.ReadLoopCts.CancelAsync();
        ctx.ReadLoopCts = new CancellationTokenSource();
        ctx.CurrentRoomId = roomId;

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

    private async Task ReadLoopAsync(UserChatContext ctx, IReadOnlyList<string> channels, CancellationToken ct)
    {
        try
        {
            await foreach (var msg in chatEventStream.ReadAsync(channels, "0-0", ct))
                ctx.Outbound.Writer.TryWrite(msg);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception e)
        {
            logger.LogError(e, "Chat subscription read loop failed for user {UserId}", ctx.UserId);
            throw;
        }
        finally
        {
            if (ctx.Cts.IsCancellationRequested)
                ctx.Outbound.Writer.TryComplete();
        }
    }
}
