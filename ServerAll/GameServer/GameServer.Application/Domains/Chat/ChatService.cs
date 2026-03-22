using System.Collections.Concurrent;
using GameServer.Application.Common;
using GameServer.Application.Domains.Chat.Interfaces;
using GameServer.Application.Domains.User.Interfaces;
using GameServer.Domain.Entities.Chat;
using Microsoft.Extensions.Logging;

namespace GameServer.Application.Domains.Chat;

public class ChatService(
    IChatMessageRepository chatMessageRepository,
    IUserSessionRepository userSessionRepository,
    IChatEventStream chatEventStream,
    ILogger<ChatService> logger) : IChatService
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _userLocks = new();

    public async Task<Result<ChatMessage>> SendMessageAsync(
        string sessionId,
        string message,
        string? targetUserNickName,
        CancellationToken ct = default)
    {
        var semaphore = _userLocks.GetOrAdd(sessionId, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(ct);

        try
        {
            var userSession = await userSessionRepository.GetBySessionIdAsync(sessionId, ct);
            if (userSession is null)
                return Result<ChatMessage>.Failure(ErrorCodes.InvalidRequest, ErrorMessages.InvalidRequest);

            var chatType =
                !string.IsNullOrWhiteSpace(targetUserNickName) ? ChatType.Whisper :
                userSession.CurrentRoomId > 0 ? ChatType.Room :
                ChatType.Global;

            long? roomId = chatType == ChatType.Room ? userSession.CurrentRoomId : null;

            var chatMessage = await chatMessageRepository.CreateAsync(
                userSession.NickName,
                chatType,
                message,
                roomId,
                targetUserNickName,
                ct);

            var channel = ChatChannels.GetChannel(chatType, roomId, targetUserNickName);
            await chatEventStream.PublishAsync(channel, chatMessage, ct);

            return Result<ChatMessage>.Success(chatMessage);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to send chat message");
            return Result<ChatMessage>.Failure(ErrorCodes.InternalServerError, ErrorMessages.InternalServerError);
        }
        finally
        {
            semaphore.Release();
        }
    }

    public async Task<IEnumerable<ChatMessage>> GetMessagesAfterAsync(
        string sessionId,
        long afterMessageId,
        CancellationToken ct = default)
    {
        var userSession = await userSessionRepository.GetBySessionIdAsync(sessionId, ct);
        if (userSession is null) return Array.Empty<ChatMessage>();

        return await chatMessageRepository.GetMessagesAfterAsync(
            afterMessageId,
            userSession.NickName,
            userSession.CurrentRoomId > 0 ? userSession.CurrentRoomId : null,
            ct);
    }

    public async Task<Result<ChatMessage>> GetMessageByIdAsync(
        string sessionId,
        long messageId,
        CancellationToken ct = default)
    {
        var userSession = await userSessionRepository.GetBySessionIdAsync(sessionId, ct);
        if (userSession is null)
            return Result<ChatMessage>.Failure(ErrorCodes.InvalidRequest, ErrorMessages.InvalidRequest);

        var message = await chatMessageRepository.GetMessageByIdAsync(messageId, ct);
        if (message is null)
            return Result<ChatMessage>.Failure(ErrorCodes.MessageNotFound, ErrorMessages.MessageNotFound);

        return Result<ChatMessage>.Success(message);
    }

    public async Task<Result<List<ChatMessage>>> GetMessagesByRoomAsync(
        string sessionId,
        long roomId,
        int limit = 50,
        long? beforeMessageId = null,
        CancellationToken ct = default)
    {
        var userSession = await userSessionRepository.GetBySessionIdAsync(sessionId, ct);
        if (userSession is null)
            return Result<List<ChatMessage>>.Failure(ErrorCodes.InvalidRequest, ErrorMessages.InvalidRequest);

        var messages = await chatMessageRepository.GetMessagesByRoomIdAsync(roomId, limit, beforeMessageId, ct);
        return Result<List<ChatMessage>>.Success(messages.ToList());
    }

    public async Task<Result<List<ChatMessage>>> GetMessagesByUserAsync(
        string sessionId,
        string userName,
        int limit = 50,
        long? beforeMessageId = null,
        CancellationToken ct = default)
    {
        var userSession = await userSessionRepository.GetBySessionIdAsync(sessionId, ct);
        if (userSession is null)
            return Result<List<ChatMessage>>.Failure(ErrorCodes.InvalidRequest, ErrorMessages.InvalidRequest);

        var messages = await chatMessageRepository.GetMessagesByUserNameAsync(userName, limit, beforeMessageId, ct);
        return Result<List<ChatMessage>>.Success(messages.ToList());
    }
}
