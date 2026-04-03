using System.Collections.Concurrent;
using GameServer.Application.Common;
using GameServer.Application.Common.Interfaces;
using GameServer.Application.Domains.Chat.Interfaces;
using GameServer.Application.Domains.DungeonLobby.Interfaces;
using GameServer.Application.Domains.User.Interfaces;
using GameServer.Domain.Entities.Chat;
using Microsoft.AspNetCore.Mvc.Filters;

namespace GameServer.Application.Domains.Chat;

public class ChatService(
    IChatMessageRepository chatMessageRepository,
    IUserSessionRepository userSessionRepository,
    IDungeonRoomRepository dungeonRoomRepository,
    IProfanityFilter profanityFilter,
    IUserLock userLock,
    IChatEventStream chatEventStream) : IChatService
{
    public async Task<Result<ChatMessage>> SendMessageAsync(
        string sessionId,
        string message,
        string? targetUserNickName,
        CancellationToken ct = default)
    {
        var userSession = await userSessionRepository.GetBySessionIdAsync(sessionId, ct);
        if (userSession is null)
            return Result<ChatMessage>.Failure(ErrorCodes.InvalidRequest, ErrorMessages.InvalidRequest);

        await using var _ = await userLock.AcquireAsync($"chat:user:{userSession.UserId}", ct);
        
        var currentRoom = await dungeonRoomRepository.GetByUserIdAsync(userSession.UserId, ct);
        var chatType = !string.IsNullOrWhiteSpace(targetUserNickName) ? ChatType.Whisper :
            currentRoom is not null ? ChatType.Room :
            ChatType.Global;

        long? roomId = chatType == ChatType.Room ? currentRoom?.RoomId : null;

        var filteredMessage = profanityFilter.Filter(message);
            
        var chatMessage = await chatMessageRepository.CreateAsync(
            userSession.NickName,
            chatType,
            filteredMessage,
            roomId,
            targetUserNickName,
            ct);

        var channel = ChatChannels.GetChannel(chatType, roomId, targetUserNickName);
        await chatEventStream.PublishAsync(channel, chatMessage, ct);

        return Result<ChatMessage>.Success(chatMessage);
    }

    public async Task<IEnumerable<ChatMessage>> GetMessagesAfterAsync(
        string sessionId,
        long afterMessageId,
        CancellationToken ct = default)
    {
        var userSession = await userSessionRepository.GetBySessionIdAsync(sessionId, ct);
        if (userSession is null) return Array.Empty<ChatMessage>();

        var currentRoom = await dungeonRoomRepository.GetByUserIdAsync(userSession.UserId, ct);
        
        return await chatMessageRepository.GetMessagesAfterAsync(
            afterMessageId,
            userSession.NickName,
            currentRoom?.RoomId,
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
