using System.Globalization;
using GameServer.Domain.Entities.Chat;
using GameServer.Infrastructure.Interfaces.Chat;
using StackExchange.Redis;

namespace GameServer.Infrastructure.Repositories.Chat;

public class ChatMessageRepository(IConnectionMultiplexer connectionMultiplexer) : IChatMessageRepository
{
    private readonly IDatabase _database = connectionMultiplexer.GetDatabase();

    private const string ChatMessageKey = "game:chat:message";
    private const string ChatMessageCounterKey = "game:chat:message:id:counter";
    private const string ChatMessageByRoomIdKey = "game:chat:message:room:{0}";
    private const string ChatMessageByUserIdKey = "game:chat:message:user:{0}";
    private const string ChatMessageByTargetUserIdKey = "game:chat:message:target:{0}";

    public async Task<ChatMessage> CreateAsync(
        long senderId,
        string senderName,
        ChatType chatType,
        string message,
        long? roomId,
        long? targetUserId,
        string? targetUserName)
    {
        try
        {
            var chatMessage = ChatMessage.Create(senderId, senderName, chatType, message, roomId, targetUserId,
                targetUserName);

            var messageId = _database.StringIncrement(ChatMessageCounterKey);
            chatMessage.SetMessageId(messageId);

            var transaction = _database.CreateTransaction();

            var hashFields = new List<HashEntry>
            {
                new HashEntry("MessageId", messageId),
                new HashEntry("SenderId", senderId),
                new HashEntry("SenderName", senderName),
                new HashEntry("ChatType", chatType.ToString()),
                new HashEntry("Message", message),
                new HashEntry("SentAt", chatMessage.SentAt.ToString("O")),
                new HashEntry("RoomId", roomId),
            };
            // 귓속말 채팅
            if (chatType == ChatType.Whisper)
            {
                if(!targetUserId.HasValue)
                    throw new ArgumentException("TargetUserId is required for whisper", nameof(targetUserId));
                hashFields.Add(new HashEntry("TargetUserId", targetUserId));
            }
            // Room 채팅
            if (chatType == ChatType.Room)
            {
                if(!roomId.HasValue)
                    throw new ArgumentException("RoomId is required for room chat", nameof(roomId));
                hashFields.Add(new HashEntry("TargetUserName", targetUserName));
            }
            
            Task hashTask = transaction.HashSetAsync($"{ChatMessageKey}:{messageId}",hashFields.ToArray());
            Task roomTask = transaction.SetAddAsync(string.Format(ChatMessageByRoomIdKey, roomId), messageId);
            Task userTask = transaction.SetAddAsync(string.Format(ChatMessageByUserIdKey, senderId), messageId);

            Task targetUserTask =
                transaction.SetAddAsync(string.Format(ChatMessageByTargetUserIdKey, targetUserId), messageId);

            bool committed = await transaction.ExecuteAsync();
            if (!committed)
            {
                throw new InvalidOperationException("Failed to create session");
            }

            await Task.WhenAll(hashTask, roomTask, userTask, targetUserTask);
            return chatMessage;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    public async Task<ChatMessage?> GetMessageByIdAsync(long messageId)
    {
        try
        {
            var entries = await _database.HashGetAllAsync($"{ChatMessageKey}:{messageId}");
            if (entries.Length == 0)
                return null;
            return ParseChatMessageFromEntries(messageId, entries);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    public async Task<IEnumerable<ChatMessage>> GetAllMessagesAsync()
    {
        try
        {
            var messageIds = await _database.SetMembersAsync(ChatMessageKey);
            if (messageIds.Length == 0)
                return Enumerable.Empty<ChatMessage>();

            var batch = _database.CreateBatch();
            var tasks = messageIds
                .Select(id => batch.HashGetAllAsync(string.Format(ChatMessageKey, id)))
                .ToList();
            batch.Execute();

            var messages = new List<ChatMessage>();
            for (int i = 0; i < messageIds.Length; i++)
            {
                var entries = await tasks[i];
                if (entries.Length == 0)
                    continue;

                if (!long.TryParse(messageIds[i].ToString(), out var messageId))
                {
                    Console.WriteLine($"Message {messageId} has missing fields");
                    continue;
                }

                var message = ParseChatMessageFromEntries(messageId, entries);
                if (message is not null)
                    messages.Add(message);
            }

            return messages;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    public async Task<IEnumerable<ChatMessage>> GetMessagesByUserIdAsync(long userId, int limit, long? beforeMessageId)
    {
        try
        {
            var userMessageIds = await _database.SetMembersAsync(string.Format(ChatMessageByUserIdKey, userId));
            if (userMessageIds.Length == 0)
                return Enumerable.Empty<ChatMessage>();

            var messages = new List<ChatMessage>();
            for (int i = 0; i < userMessageIds.Length; i++)
            {
                if (!long.TryParse(userMessageIds[i].ToString(), out var messageId))
                    continue;
                var message = await GetMessageByIdAsync(messageId);
                if (message is not null)
                    messages.Add(message);
            }
            return messages;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    public async Task<IEnumerable<ChatMessage>> GetMessagesByRoomIdAsync(long roomId, int limit, long? beforeMessageId)
    {
        try
        {
            var roomMessageIds = await _database.SetMembersAsync(string.Format(ChatMessageByRoomIdKey, roomId));
            if (roomMessageIds.Length == 0)
                return Enumerable.Empty<ChatMessage>();

            var messages = new List<ChatMessage>();
            for (int i = 0; i < roomMessageIds.Length; i++)
            {
                if (!long.TryParse(roomMessageIds[i].ToString(), out var messageId))
                    continue;
                var message = await GetMessageByIdAsync(messageId);
                if (message is not null)
                    messages.Add(message);
            }
            return messages;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    public async Task<bool> DeleteAsync(long messageId)
    {
        try
        {
            var message = await GetMessageByIdAsync(messageId);
            if (message is null)
                return false;
            
            var transaction = _database.CreateTransaction();
            
            Task hashTask = transaction.KeyDeleteAsync($"{ChatMessageKey}:{messageId}");
            Task roomTask = transaction.SetRemoveAsync(string.Format(ChatMessageByRoomIdKey, message.RoomId), messageId);
            Task userTask = transaction.SetRemoveAsync(string.Format(ChatMessageByUserIdKey, message.SenderUserId), messageId);
            Task targetUserTask = transaction.SetRemoveAsync(string.Format(ChatMessageByTargetUserIdKey, message.TargetUserId), messageId);
            
            bool committed = await transaction.ExecuteAsync();
            if (!committed)
                return false;
        
            await Task.WhenAll(hashTask, roomTask, userTask, targetUserTask);
            return true;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    public async Task<bool> DeleteAllAsync()
    {
        try
        {
            var transaction = _database.CreateTransaction();
                    
            Task counterTask = transaction.KeyDeleteAsync(ChatMessageCounterKey);
            
            var server = connectionMultiplexer.GetServer(connectionMultiplexer.GetEndPoints().First());
            var keys = server.Keys(pattern: $"{ChatMessageKey}:*");
            
            var deleteTasks = keys.Select(key => transaction.KeyDeleteAsync(key)).ToList();
            
            bool committed = await transaction.ExecuteAsync();
            if (!committed)
                return false;
            
            await Task.WhenAll(deleteTasks.Concat([counterTask]));
            return true;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }
    public async Task<bool> DeleteByUserIdAsync(long userId)
    {
        try
        {
            var userMessageIds = await _database.SetMembersAsync(string.Format(ChatMessageByUserIdKey, userId));
            if (userMessageIds.Length == 0)
                return true;
    
            var transaction = _database.CreateTransaction();
        
            var deleteTasks = new List<Task>();
        
            foreach (var messageIdValue in userMessageIds)
            {
                if (!long.TryParse(messageIdValue.ToString(), out var messageId))
                    continue;
                
                var message = await GetMessageByIdAsync(messageId);
                if (message is null)
                    continue;
            
                deleteTasks.Add(transaction.KeyDeleteAsync($"{ChatMessageKey}:{messageId}"));
                deleteTasks.Add(transaction.SetRemoveAsync(string.Format(ChatMessageByRoomIdKey, message.RoomId), messageId));
                deleteTasks.Add(transaction.SetRemoveAsync(string.Format(ChatMessageByTargetUserIdKey, message.TargetUserId), messageId));
            }
        
            deleteTasks.Add(transaction.KeyDeleteAsync(string.Format(ChatMessageByUserIdKey, userId)));
        
            bool committed = await transaction.ExecuteAsync();
            if (!committed)
                return false;
        
            await Task.WhenAll(deleteTasks);
            return true;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }
    
    public async Task<bool> DeleteByRoomIdAsync(long roomId)
    {
        try
        {
            var roomMessageIds = await _database.SetMembersAsync(string.Format(ChatMessageByRoomIdKey, roomId));
            if (roomMessageIds.Length == 0)
                return true;
    
            var transaction = _database.CreateTransaction();
        
            var deleteTasks = new List<Task>();
        
            foreach (var messageIdValue in roomMessageIds)
            {
                if (!long.TryParse(messageIdValue.ToString(), out var messageId))
                    continue;
                
                var message = await GetMessageByIdAsync(messageId);
                if (message is null)
                    continue;
            
                deleteTasks.Add(transaction.KeyDeleteAsync($"{ChatMessageKey}:{messageId}"));
                deleteTasks.Add(transaction.SetRemoveAsync(string.Format(ChatMessageByUserIdKey, message.SenderUserId), messageId));
                deleteTasks.Add(transaction.SetRemoveAsync(string.Format(ChatMessageByTargetUserIdKey, message.TargetUserId), messageId));
            }
        
            deleteTasks.Add(transaction.KeyDeleteAsync(string.Format(ChatMessageByRoomIdKey, roomId)));
        
            bool committed = await transaction.ExecuteAsync();
            if (!committed)
                return false;
        
            await Task.WhenAll(deleteTasks);
            return true;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    private ChatMessage? ParseChatMessageFromEntries(long messageId, HashEntry[] entries)
    {
        var dict = entries.ToDictionary(
            x => x.Name.ToString(),
            x => x.Value.ToString()
        );

        // 필수 필드 검증
        if (!dict.TryGetValue("SenderId", out var senderIdStr) ||
            !dict.TryGetValue("SenderName", out var senderName) ||
            !dict.TryGetValue("ChatType", out var chatTypeStr) ||
            !dict.TryGetValue("Message", out var message) ||
            !dict.TryGetValue("SentAt", out var sentAtStr)
           )
        {
            Console.WriteLine($"Message {messageId} has missing fields");
            return null;
        }

        if (!long.TryParse(senderIdStr, out var senderId))
        {
            Console.WriteLine("Invalid SenderId");
            return null;
        }

        if (!Enum.TryParse<ChatType>(chatTypeStr, out var chatType))
        {
            Console.WriteLine($"Invalid ChatType: {chatTypeStr}");
            return null;
        }


        if (!DateTime.TryParse(sentAtStr, null, DateTimeStyles.RoundtripKind, out var sentAt))
        {
            Console.WriteLine($"Invalid SentAt: {sentAtStr}");
            return null;
        }

        long? roomId = null;
        if (chatType == ChatType.Room && !dict.TryGetValue("RoomId", out var roomIdStr))
        {
            if (!long.TryParse(roomIdStr, out var id))
            {
                Console.WriteLine("Invalid RoomId");
                return null;
            }
            roomId = id;
        }
        
        long? targetUserId = null;
        if(chatType == ChatType.Whisper && !dict.TryGetValue("TargetUserId", out var targetUserIdStr))
        {
            if (!long.TryParse(targetUserIdStr, out var id))
            {
                Console.WriteLine("Invalid TargetUserId");
                return null;
            }
            targetUserId = id;
        };

        return ChatMessage.FromRedis(messageId,
            senderId,
            senderName,
            chatType,
            message,
            sentAt,
            roomId,
            targetUserId);
    }
}