using System.Collections.Concurrent;
using GameServer.Domain.Entities.Chat;
using GameServer.Infrastructure.Interfaces.Chat;

namespace GameServer.Tests.Infrastructure;

public class FakeChatMessageRepository : IChatMessageRepository
{
    private readonly ConcurrentDictionary<long, ChatMessage> _messages = new();
    private long _nextMessageId = 1;


    public Task<ChatMessage> CreateAsync(long senderId, string senderName, ChatType chatType, string message, long? roomId, long? targetUserId)
    {
        var chatMessage = ChatMessage.Create(senderId, senderName, chatType, message, roomId, targetUserId);
        var messageId = Interlocked.Increment(ref _nextMessageId);
        chatMessage.SetMessageId(messageId);
        
        _messages[messageId] = chatMessage;
        return Task.FromResult(chatMessage);
    }

    public Task<ChatMessage?> GetMessageByIdAsync(long messageId)
    {
        _messages.TryGetValue(messageId, out var message);
        return Task.FromResult(message);
    }

    public Task<IEnumerable<ChatMessage>> GetAllMessagesAsync()
    {
        return Task.FromResult(_messages.Values.AsEnumerable());
    }

    public Task<IEnumerable<ChatMessage>> GetMessagesByUserIdAsync(long userId, int limit, long? beforeMessageId)
    {
        var query = _messages.Values
            .Where(m => m.SenderUserId == userId || m.TargetUserId == userId);

        if (beforeMessageId.HasValue)
        {
            query = query.Where(m => m.MessageId < beforeMessageId.Value);
        }

        var result = query.OrderByDescending(m => m.MessageId)
            .Take(limit)
            .ToList();

        return Task.FromResult<IEnumerable<ChatMessage>>(result);
    }

    public Task<IEnumerable<ChatMessage>> GetMessagesByRoomIdAsync(long roomId, int limit, long? beforeMessageId)
    {
        var query = _messages.Values
            .Where(m => m.RoomId == roomId);

        if (beforeMessageId.HasValue)
        {
            query = query.Where(m => m.MessageId < beforeMessageId.Value);
        }

        var result = query.OrderByDescending(m => m.MessageId)
            .Take(limit)
            .ToList();

        return Task.FromResult<IEnumerable<ChatMessage>>(result);
    }

    public Task<bool> DeleteAsync(long messageId)
    {
        return Task.FromResult(_messages.TryRemove(messageId, out _));
    }

    public Task<bool> DeleteAllAsync()
    {
        _messages.Clear();
        return Task.FromResult(true);
    }

    public Task<bool> DeleteByUserIdAsync(long userId)
    {
        var messagesToRemove = _messages.Values
            .Where(m => m.SenderUserId == userId)
            .ToList();

        bool anyRemoved = false;
        foreach (var message in messagesToRemove)
        {
            if (_messages.TryRemove(message.MessageId, out _))
            {
                anyRemoved = true;
            }
        }

        return Task.FromResult(anyRemoved);
    }

    public Task<bool> DeleteByRoomIdAsync(long roomId)
    {
        var messagesToRemove = _messages.Values
            .Where(m => m.RoomId == roomId)
            .ToList();

        bool anyRemoved = false;
        foreach (var message in messagesToRemove)
        {
            if (_messages.TryRemove(message.MessageId, out _))
            {
                anyRemoved = true;
            }
        }

        return Task.FromResult(anyRemoved);
    }
}