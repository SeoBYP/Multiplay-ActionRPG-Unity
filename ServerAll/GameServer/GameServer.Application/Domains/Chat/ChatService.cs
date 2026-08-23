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
    IUserProfileRepository userProfileRepository,
    IDungeonRoomRepository dungeonRoomRepository,
    IDungeonRoomPlayerRepository dungeonRoomPlayerRepository,
    IProfanityFilter profanityFilter,
    IDistributedLock distributedLock,
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

        var userProfile = await userProfileRepository.GetByIdAsync(userSession.UserId, ct);
        if (userProfile is null)
            return Result<ChatMessage>.Failure(ErrorCodes.InvalidRequest, ErrorMessages.InvalidRequest);
        
        await using var _ = await distributedLock.AcquireAsync($"chat:user:{userSession.UserId}", ct);
        
        var roomPlayer = await dungeonRoomPlayerRepository.GetByUserIdAsync(userSession.UserId, ct);
        var currentRoom = roomPlayer is null
            ? null
            : await dungeonRoomRepository.GetByIdAsync(roomPlayer.RoomId, ct);
        var chatType = !string.IsNullOrWhiteSpace(targetUserNickName) ? ChatType.Whisper :
            currentRoom is not null ? ChatType.Room :
            ChatType.Global;

        long? roomId = chatType == ChatType.Room ? currentRoom?.RoomId : null;

        var filteredMessage = profanityFilter.Filter(message);

        try
        {
            ChatMessage.Validate(
                userProfile.NickName,
                chatType,
                filteredMessage,
                roomId,
                targetUserNickName);
        }
        catch (ArgumentException)
        {
            return Result<ChatMessage>.Failure(ErrorCodes.InvalidRequest, ErrorMessages.InvalidRequest);
        }
            
        var chatMessage = await chatMessageRepository.CreateAsync(
            userProfile.NickName,
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
        
        var userProfile = await userProfileRepository.GetByIdAsync(userSession.UserId, ct);
        if (userProfile is null) return Array.Empty<ChatMessage>();

        var roomPlayer = await dungeonRoomPlayerRepository.GetByUserIdAsync(userSession.UserId, ct);
        var currentRoom = roomPlayer is null
            ? null
            : await dungeonRoomRepository.GetByIdAsync(roomPlayer.RoomId, ct);
        
        var messages = await chatMessageRepository.GetMessagesAfterAsync(
            afterMessageId,
            ct);

        return messages.Where(m =>
            m.ChatType == ChatType.Global ||
            (m.ChatType == ChatType.Room && m.RoomId == currentRoom?.RoomId) ||
            (m.ChatType == ChatType.Whisper && (m.SenderUserNickName == userProfile.NickName || m.TargetUserNickName == userProfile.NickName))
        );
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
