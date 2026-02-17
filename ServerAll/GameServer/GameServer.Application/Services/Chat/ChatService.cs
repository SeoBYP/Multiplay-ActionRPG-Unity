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
    public async Task<Result<ChatMessage>> SendMessageAsync(string sessionId, ChatType chatType, string message, long? roomId, long? targetUserId,
        CancellationToken ct = default)
    {
        try
        {
            var userSession = await userSessionRepository.GetBySessionIdAsync(sessionId);

            if(userSession is null)
                return Result<ChatMessage>.Failure(ErrorCodes.InvalidRequest, ErrorMessages.InvalidRequest);

            var chatMessage = await chatMessageRepository.CreateAsync(userSession.UserId, 
                userSession.UserName,
                chatType, 
                message, 
                roomId,
                targetUserId);
            
            var channel = ChatChannels.GetChannel(chatType, roomId, targetUserId);
            var json = JsonSerializer.Serialize(chatMessage);
            await redis.GetSubscriber().PublishAsync(channel, json);
            
            return Result<ChatMessage>.Success(chatMessage);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return Result<ChatMessage>.Failure(ErrorCodes.InternalServerError,ErrorMessages.InternalServerError);
        }
    }

    public async Task<Result<ChatMessage>> GetMessageByIdAsync(string sessionId, long messageId, CancellationToken ct = default)
    {
        try
        {
            var userSession = await userSessionRepository.GetBySessionIdAsync(sessionId);

            if(userSession is null)
                return Result<ChatMessage>.Failure(ErrorCodes.InvalidRequest, ErrorMessages.InvalidRequest);

            var chatMessage = await chatMessageRepository.GetMessageByIdAsync(messageId);
            if(chatMessage is null)
                return Result<ChatMessage>.Failure(ErrorCodes.MessageNotFound, ErrorMessages.MessageNotFound);
            return Result<ChatMessage>.Success(chatMessage);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return Result<ChatMessage>.Failure(ErrorCodes.InternalServerError,ErrorMessages.InternalServerError);
        }
    }

    public async Task<Result<IReadOnlyList<ChatMessage>>> GetMessagesByRoomAsync(string sessionId, long roomId, int limit = 50, long? beforeMessageId = null,
        CancellationToken ct = default)
    {
        try
        {
            var userSession = await userSessionRepository.GetBySessionIdAsync(sessionId);
            if(userSession is null)
                return Result<IReadOnlyList<ChatMessage>>.Failure(ErrorCodes.InvalidRequest, ErrorMessages.InvalidRequest);
            
            var chatMessages = await chatMessageRepository.GetMessagesByRoomIdAsync(roomId, limit, beforeMessageId);
            return Result<IReadOnlyList<ChatMessage>>.Success(chatMessages.ToList());
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return Result<IReadOnlyList<ChatMessage>>.Failure(ErrorCodes.InternalServerError,ErrorMessages.InternalServerError);
        }
    }

    public async Task<Result<IReadOnlyList<ChatMessage>>> GetMessagesByUserAsync(string sessionId, long userId, int limit = 50, long? beforeMessageId = null,
        CancellationToken ct = default)
    {
        try
        {
            var userSession = await userSessionRepository.GetBySessionIdAsync(sessionId);
            if(userSession is null)
                return Result<IReadOnlyList<ChatMessage>>.Failure(ErrorCodes.InvalidRequest, ErrorMessages.InvalidRequest);
            
            var chatMessages = await chatMessageRepository.GetMessagesByUserIdAsync(userId, limit, beforeMessageId);
            return Result<IReadOnlyList<ChatMessage>>.Success(chatMessages.ToList());
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return Result<IReadOnlyList<ChatMessage>>.Failure(ErrorCodes.InternalServerError,ErrorMessages.InternalServerError);
        }
    }
}