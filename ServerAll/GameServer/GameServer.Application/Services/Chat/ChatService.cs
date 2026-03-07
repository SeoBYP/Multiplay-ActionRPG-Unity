using System.Text.Json;
using System.Collections.Generic;
using System.Linq;
using GameServer.Application.Common;
using GameServer.Application.Services.Chat.Interfaces;
using GameServer.Domain.Entities;
using GameServer.Domain.Entities.Chat;
using GameServer.Infrastructure.Interfaces.Chat;
using GameServer.Infrastructure.Interfaces.User;
using StackExchange.Redis;

namespace GameServer.Application.Services.Chat;

public class ChatService(IChatMessageRepository chatMessageRepository,
    IUserSessionRepository userSessionRepository,
    IConnectionMultiplexer redis) : IChatService
{
    public async Task<Result<ChatMessage>> SendMessageAsync(
        string sessionId,
        string message,
        string? targetUserNickName,
        CancellationToken ct = default)
    {
        try
        {
            var userSession = await userSessionRepository.GetBySessionIdAsync(sessionId, ct);
            if (userSession is null)
                return Result<ChatMessage>.Failure(ErrorCodes.InvalidRequest, ErrorMessages.InvalidRequest);

            // ChatType 자동 결정
            var chatType =
                !string.IsNullOrWhiteSpace(targetUserNickName) ? ChatType.Whisper :
                userSession.CurrentRoomId > 0 ? ChatType.Room :
                ChatType.Global;

            long? roomId = chatType == ChatType.Room ? userSession.CurrentRoomId : null;

            // 저장(메시지ID/히스토리 등)
            var chatMessage = await chatMessageRepository.CreateAsync(
                userSession.NickName,
                chatType,
                message,
                roomId,
                targetUserNickName,
                ct);

            // Redis publish
            var channel = ChatChannels.GetChannel(chatType, roomId, targetUserNickName);
            var json = JsonSerializer.Serialize(chatMessage);
            await redis.GetSubscriber().PublishAsync(channel, json);

            return Result<ChatMessage>.Success(chatMessage);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return Result<ChatMessage>.Failure(ErrorCodes.InternalServerError, ErrorMessages.InternalServerError);
        }
    }
    public async Task<IEnumerable<ChatMessage>> GetMessagesAfterAsync(
        string sessionId,
        long afterMessageId,
        CancellationToken ct = default)
    {
        var userSession = await userSessionRepository.GetBySessionIdAsync(sessionId, ct);
        if (userSession is null) return Array.Empty<ChatMessage>();

        // (UserId, MessageId 기준 필터링) - Global, 현재 Room, 본인 관련 Whisper
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