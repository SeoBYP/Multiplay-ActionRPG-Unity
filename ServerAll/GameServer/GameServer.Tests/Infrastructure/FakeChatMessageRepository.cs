using System.Collections.Concurrent;
using GameServer.Domain.Entities.Chat;
using GameServer.Infrastructure.Interfaces.Chat;

namespace GameServer.Tests.Infrastructure;

public class FakeChatMessageRepository : IChatMessageRepository
{
    private readonly ConcurrentDictionary<long, ChatMessage> _messages = new();
    private long _nextMessageId = 1;


    public Task<ChatMessage> CreateAsync(string senderName, ChatType chatType, string message, long? roomId, string? targetUserNickName)
    {
        var chatMessage = ChatMessage.Create(senderName, chatType, message, roomId, targetUserNickName);
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

    public Task<IEnumerable<ChatMessage>> GetMessagesByUserNameAsync(string userName, int limit, long? beforeMessageId)
    {
        var query = _messages.Values
            .Where(m => m.SenderUserName == userName || m.TargetUserNickName == userName);

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

    public Task<bool> DeleteByUserNameAsync(string userName)
    {
        var messagesToRemove = _messages.Values
            .Where(m => m.SenderUserName == userName)
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