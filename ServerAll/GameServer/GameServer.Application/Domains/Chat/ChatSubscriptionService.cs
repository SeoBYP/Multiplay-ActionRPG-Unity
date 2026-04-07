using System.Collections.Concurrent;
using GameServer.Application.Domains.Chat.Interfaces;
using GameServer.Application.Domains.DungeonLobby.Interfaces;
using GameServer.Application.Domains.User.Interfaces;
using Microsoft.Extensions.Logging;

namespace GameServer.Application.Domains.Chat;

public sealed class ChatSubscriptionService(
    IChatEventStream chatEventStream,
    IUserSessionRepository userSessionRepository,
    IUserProfileRepository userProfileRepository,
    IDungeonRoomRepository dungeonRoomRepository,
    IDungeonRoomPlayerRepository dungeonRoomPlayerRepository,
    ILogger<ChatSubscriptionService> logger) : IChatSubscriptionService
{
    private readonly ConcurrentDictionary<long, UserChatContext> _contexts = new();

    public async Task<UserChatContext?> ConnectAsync(string sessionId, CancellationToken ct)
    {
        var userSession = await userSessionRepository.GetBySessionIdAsync(sessionId, ct);
        if (userSession is null) return null;

        var userProfile = await userProfileRepository.GetByIdAsync(userSession.UserId, ct);
        if (userProfile is null) return null;

        var roomPlayer = await dungeonRoomPlayerRepository.GetByUserIdAsync(userSession.UserId, ct);
        var currentRoom = roomPlayer is null
            ? null
            : await dungeonRoomRepository.GetByIdAsync(roomPlayer.RoomId, ct);
        
        var ctx = new UserChatContext(userSession.UserId, userProfile.NickName, currentRoom?.RoomId ?? 0);

        if (_contexts.TryGetValue(userSession.UserId, out var existing))
        {
            await DisconnectAsync(existing, ct);
        }

        _contexts[userSession.UserId] = ctx;

        var list = new List<string> { ChatChannels.GlobalChannel, ChatChannels.WhisperChannel(ctx.Nickname) };
        if (ctx.CurrentRoomId != 0)
            list.Add(ChatChannels.RoomChannel(ctx.CurrentRoomId));

        _ = Task.Run(() => ReadLoopAsync(ctx, list, ctx.ReadLoopCts.Token));

        return ctx;
    }

    public async Task UpdateRoomSubscriptionAsync(string sessionId, long roomId, CancellationToken ct)
    {
        var session = await userSessionRepository.GetBySessionIdAsync(sessionId, ct);
        if (session is null) return;

        if (!_contexts.TryGetValue(session.UserId, out var ctx))
            return;
        
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
