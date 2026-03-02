using System.Text.Json;
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

        // repo에 필요: after id 이후 메시지
        return await chatMessageRepository.GetMessagesAfterAsync(afterMessageId, ct);
    }
}