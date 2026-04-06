using System.Collections.Concurrent;
using GameServer.Application.Domains.Chat.Interfaces;
using GameServer.Domain.Entities.Chat;

namespace GameServer.Tests.Infrastructure.Fakes.Repositories;

public class FakeChatMessageRepository : IChatMessageRepository
{
    private readonly ConcurrentDictionary<long, ChatMessage> _messages = new();
    private long _nextMessageId = 1;


    public Task<IEnumerable<ChatMessage>> GetMessagesAfterAsync(long afterMessageId, CancellationToken ct = default)
    {
        var result = _messages.Values
            .Where(m => m.MessageId > afterMessageId)
            .OrderBy(m => m.MessageId)
            .ToList();
        return Task.FromResult<IEnumerable<ChatMessage>>(result);
    }

    public Task<ChatMessage> CreateAsync(string senderName, ChatType chatType, string message, long? roomId, string? targetUserNickName, CancellationToken ct = default)
    {
        var messageId = Interlocked.Increment(ref _nextMessageId);
        var chatMessage = ChatMessage.CreateNew(messageId, senderName, chatType, message, roomId, targetUserNickName);
        
        _messages[messageId] = chatMessage;
        return Task.FromResult(chatMessage);
    }

    public Task<ChatMessage?> GetMessageByIdAsync(long messageId, CancellationToken ct = default)
    {
        _messages.TryGetValue(messageId, out var message);
        return Task.FromResult(message);
    }

    public Task<IEnumerable<ChatMessage>> GetAllMessagesAsync(CancellationToken ct = default)
    {
        return Task.FromResult(_messages.Values.AsEnumerable());
    }

    public Task<IEnumerable<ChatMessage>> GetMessagesByUserNameAsync(string userName, int limit, long? beforeMessageId, CancellationToken ct = default)
    {
        var query = _messages.Values
            .Where(m => m.SenderUserNickName == userName || m.TargetUserNickName == userName);

        if (beforeMessageId.HasValue)
        {
            query = query.Where(m => m.MessageId < beforeMessageId.Value);
        }

        var result = query.OrderByDescending(m => m.MessageId)
            .Take(limit)
            .ToList();

        return Task.FromResult<IEnumerable<ChatMessage>>(result);
    }

    public Task<IEnumerable<ChatMessage>> GetMessagesByRoomIdAsync(long roomId, int limit, long? beforeMessageId, CancellationToken ct = default)
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

    public Task<bool> DeleteAsync(long messageId, CancellationToken ct = default)
    {
        return Task.FromResult(_messages.TryRemove(messageId, out _));
    }

    public Task<bool> DeleteAllAsync(CancellationToken ct = default)
    {
        _messages.Clear();
        return Task.FromResult(true);
    }

    public Task<bool> DeleteByUserNameAsync(string userName, CancellationToken ct = default)
    {
        var messagesToRemove = _messages.Values
            .Where(m => m.SenderUserNickName == userName)
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

    public Task<bool> DeleteByRoomIdAsync(long roomId, CancellationToken ct = default)
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