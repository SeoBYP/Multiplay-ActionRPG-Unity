using System.Globalization;
using GameServer.Application.Domains.Chat.Interfaces;
using GameServer.Domain.Entities.Chat;
using GameServer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace GameServer.Infrastructure.Domains.Chat;

public class ChatMessageRepository(
    IConnectionMultiplexer connectionMultiplexer,
    GameServerDbContext context,
    ILogger<ChatMessageRepository> logger) : IChatMessageRepository
{
    private readonly IDatabase _database = connectionMultiplexer.GetDatabase();
    
    public async Task<IEnumerable<ChatMessage>> GetMessagesAfterAsync(long afterMessageId, CancellationToken ct = default)
    {
        try
        {
            var messageIds = await _database.SortedSetRangeByScoreAsync(
                RedisKeys.ChatAllMessages(),
                start: afterMessageId + 1, // afterMessageId 초과
                stop: double.MaxValue,
                order: Order.Ascending); // 오래된 것부터 (순서대로 전달)

            IEnumerable<ChatMessage> messages;
            if (messageIds.Length > 0)
            {
                messages = await FetchMessagesByIds(messageIds);
            }
            else
            {
                // Redis에 없는 경우 DB에서 조회
                messages = await context.ChatMessages
                    .AsNoTracking()
                    .Where(m => m.MessageId > afterMessageId)
                    .OrderBy(m => m.MessageId)
                    .ToListAsync(ct);

                // 조회된 메시지들을 Redis에 캐싱 (필요 시)
                if (messages.Any())
                {
                    await Task.WhenAll(messages.Select(m => SetChatMessageCacheAsync(m)));
                }
            }

            return messages;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to get messages after {MessageId}", afterMessageId);
            throw;
        }
    }

    public async Task<ChatMessage> CreateAsync(
        string senderName,
        ChatType chatType,
        string message,
        long? roomId,
        string? targetUserNickName,
        CancellationToken ct = default)
    {
        try
        {
            // 1. 도메인 엔티티 생성 (ID는 DB에서 자동 채번되므로 임시 ID 0 사용)
            var chatMessage = ChatMessage.CreateNew(
                0, // Identity column이므로 0으로 설정
                senderName,
                chatType,
                message,
                roomId,
                targetUserNickName);

            // 2. DB 저장 (PostgreSQL)
            var entry = await context.ChatMessages.AddAsync(chatMessage, ct);
            await context.SaveChangesAsync(ct);

            var createdMessage = entry.Entity;

            // 3. Redis 캐시 저장
            await SetChatMessageCacheAsync(createdMessage);

            return createdMessage;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to create chat message for sender {SenderName}", senderName);
            throw;
        }
    }

    public async Task<ChatMessage?> GetMessageByIdAsync(long messageId, CancellationToken ct = default)
    {
        try
        {
            var entries = await _database.HashGetAllAsync(RedisKeys.ChatMessage(messageId));
            if (entries.Length > 0)
                return ParseChatMessage(messageId, entries);

            var dbMessage = await context.ChatMessages.AsNoTracking().SingleOrDefaultAsync(m => m.MessageId == messageId, ct);
            if (dbMessage is not null)
            {
                await SetChatMessageCacheAsync(dbMessage);
            }

            return dbMessage;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to get chat message by id {MessageId}", messageId);
            throw;
        }
    }


    public async Task<IEnumerable<ChatMessage>> GetAllMessagesAsync(CancellationToken ct = default)
    {
        try
        {
            var messageIds = await _database.SortedSetRangeByRankAsync(
                RedisKeys.ChatAllMessages(),
                order: Order.Descending);

            if (messageIds.Length > 0)
            {
                return await FetchMessagesByIds(messageIds);
            }

            // Redis 미스 시 DB 조회
            var dbMessages = await context.ChatMessages
                .AsNoTracking()
                .OrderByDescending(m => m.MessageId)
                .Take(100) // 전체 조회 시 성능을 위해 최근 100개로 제한하는 것이 일반적
                .ToListAsync(ct);

            if (dbMessages.Count > 0)
            {
                await Task.WhenAll(dbMessages.Select(m => SetChatMessageCacheAsync(m)));
            }

            return dbMessages;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to get all chat messages");
            throw;
        }
    }

    public async Task<IEnumerable<ChatMessage>> GetMessagesByUserNameAsync(
        string userName, int limit, long? beforeMessageId, CancellationToken ct = default)
    {
        try
        {
            var maxScore = beforeMessageId.HasValue
                ? (double)beforeMessageId.Value - 1 // beforeMessageId 미만
                : double.MaxValue; // 처음 조회 시 최신부터

            var messageIds = await _database.SortedSetRangeByScoreAsync(
                RedisKeys.ChatUserIndex(userName),
                stop: maxScore,
                take: limit,
                order: Order.Descending); // 최신 메시지부터

            if (messageIds.Length > 0)
            {
                return await FetchMessagesByIds(messageIds);
            }

            // DB 폴백
            var query = context.ChatMessages
                .AsNoTracking()
                .Where(m => m.SenderUserNickName == userName);

            if (beforeMessageId.HasValue)
            {
                query = query.Where(m => m.MessageId < beforeMessageId.Value);
            }

            var dbMessages = await query
                .OrderByDescending(m => m.MessageId)
                .Take(limit)
                .ToListAsync(ct);

            if (dbMessages.Count > 0)
            {
                await Task.WhenAll(dbMessages.Select(m => SetChatMessageCacheAsync(m)));
            }

            return dbMessages;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to get chat messages by user {UserName}", userName);
            throw;
        }
    }

    public async Task<IEnumerable<ChatMessage>> GetMessagesByRoomIdAsync(
        long roomId, int limit, long? beforeMessageId, CancellationToken ct = default)
    {
        try
        {
            var maxScore = beforeMessageId.HasValue
                ? (double)beforeMessageId.Value - 1
                : double.MaxValue;

            var messageIds = await _database.SortedSetRangeByScoreAsync(
                RedisKeys.ChatRoomIndex(roomId),
                stop: maxScore,
                take: limit,
                order: Order.Descending);

            if (messageIds.Length > 0)
            {
                return await FetchMessagesByIds(messageIds);
            }

            // DB 폴백
            var query = context.ChatMessages
                .AsNoTracking()
                .Where(m => m.RoomId == roomId);

            if (beforeMessageId.HasValue)
            {
                query = query.Where(m => m.MessageId < beforeMessageId.Value);
            }

            var dbMessages = await query
                .OrderByDescending(m => m.MessageId)
                .Take(limit)
                .ToListAsync(ct);

            if (dbMessages.Count > 0)
            {
                await Task.WhenAll(dbMessages.Select(SetChatMessageCacheAsync));
            }

            return dbMessages;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to get chat messages by room {RoomId}", roomId);
            throw;
        }
    }

    public async Task<bool> DeleteAsync(long messageId, CancellationToken ct = default)
    {
        try
        {
            var message = await context.ChatMessages.SingleOrDefaultAsync(m => m.MessageId == messageId, ct);
            if (message is not null)
            {
                context.ChatMessages.Remove(message);
                await context.SaveChangesAsync(ct);
            }

            // Redis 캐시 삭제 (DB에 없더라도 일관성을 위해 캐시 삭제 시도)
            await DeleteChatMessageCacheAsync(messageId, message?.SenderUserNickName, message?.RoomId, message?.TargetUserNickName);
            return true;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to delete chat message {MessageId}", messageId);
            throw;
        }
    }

    public async Task<bool> DeleteAllAsync(CancellationToken ct = default)
    {
        try
        {
            // DB 데이터 전체 삭제
            await context.ChatMessages.ExecuteDeleteAsync(ct);

            // Redis 데이터 전체 삭제
            var server = connectionMultiplexer.GetServer(connectionMultiplexer.GetEndPoints().First());

            var keysToDelete = new List<RedisKey>();
            // 성능을 위해 SCAN 권장하지만 여기선 기존 패턴 유지
            keysToDelete.AddRange(server.Keys(pattern: "game:chat:message:*").ToArray());

            if (keysToDelete.Count == 0)
                return true;

            var transaction = _database.CreateTransaction();
            foreach (var key in keysToDelete.Distinct())
            {
                _ = transaction.KeyDeleteAsync(key);
            }

            return await transaction.ExecuteAsync();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to delete all chat messages");
            throw;
        }
    }

    public async Task<bool> DeleteByUserNameAsync(string userName, CancellationToken ct = default)
    {
        try
        {
            // DB에서 해당 유저의 메시지 조회
            var dbMessages = await context.ChatMessages
                .Where(m => m.SenderUserNickName == userName)
                .ToListAsync(ct);

            if (dbMessages.Count > 0)
            {
                context.ChatMessages.RemoveRange(dbMessages);
                await context.SaveChangesAsync(ct);
            }

            // Redis 캐시 삭제
            var userKey = RedisKeys.ChatUserIndex(userName);
            var messageIds = await _database.SortedSetRangeByRankAsync(userKey);

            var deleteTasks = new List<Task>();
            foreach (var idValue in messageIds)
            {
                if (long.TryParse(idValue.ToString(), out var messageId))
                {
                    var msg = dbMessages.FirstOrDefault(m => m.MessageId == messageId);
                    deleteTasks.Add(DeleteChatMessageCacheAsync(messageId, userName, msg?.RoomId, msg?.TargetUserNickName));
                }
            }
            deleteTasks.Add(_database.KeyDeleteAsync(userKey));

            await Task.WhenAll(deleteTasks);
            return true;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to delete chat messages by user {UserName}", userName);
            throw;
        }
    }

    public async Task<bool> DeleteByRoomIdAsync(long roomId, CancellationToken ct = default)
    {
        try
        {
            // DB에서 해당 방의 메시지 조회
            var dbMessages = await context.ChatMessages
                .Where(m => m.RoomId == roomId)
                .ToListAsync(ct);

            if (dbMessages.Count > 0)
            {
                context.ChatMessages.RemoveRange(dbMessages);
                await context.SaveChangesAsync(ct);
            }

            // Redis 캐시 삭제
            var roomKey = RedisKeys.ChatRoomIndex(roomId);
            var messageIds = await _database.SortedSetRangeByRankAsync(roomKey);

            var deleteTasks = new List<Task>();
            foreach (var idValue in messageIds)
            {
                if (long.TryParse(idValue.ToString(), out var messageId))
                {
                    var msg = dbMessages.FirstOrDefault(m => m.MessageId == messageId);
                    deleteTasks.Add(DeleteChatMessageCacheAsync(messageId, msg?.SenderUserNickName, roomId, msg?.TargetUserNickName));
                }
            }
            deleteTasks.Add(_database.KeyDeleteAsync(roomKey));

            await Task.WhenAll(deleteTasks);
            return true;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to delete chat messages by room {RoomId}", roomId);
            throw;
        }
    }
    
    /// <summary>
    /// ChatMessage 캐시 설정
    /// </summary>
    private async Task SetChatMessageCacheAsync(ChatMessage message)
    {
        var transaction = _database.CreateTransaction();

        var hashFields = new List<HashEntry>
        {
            new("MessageId", message.MessageId),
            new("SenderName", message.SenderUserNickName),
            new("ChatType", message.ChatType.ToString()),
            new("Message", message.Message),
            new("SentAt", message.SentAt.ToString("O")),
        };

        if (message.RoomId.HasValue)
            hashFields.Add(new HashEntry("RoomId", message.RoomId.Value));

        if (!string.IsNullOrWhiteSpace(message.TargetUserNickName))
            hashFields.Add(new HashEntry("TargetUserNickName", message.TargetUserNickName));

        _ = transaction.HashSetAsync(RedisKeys.ChatMessage(message.MessageId), hashFields.ToArray());
        _ = transaction.KeyExpireAsync(RedisKeys.ChatMessage(message.MessageId), RedisSettings.RedisCacheTtl);

        _ = transaction.SortedSetAddAsync(RedisKeys.ChatAllMessages(), message.MessageId, message.MessageId);
        _ = transaction.KeyExpireAsync(RedisKeys.ChatAllMessages(), RedisSettings.RedisCacheTtl);

        _ = transaction.SortedSetAddAsync(RedisKeys.ChatUserIndex(message.SenderUserNickName), message.MessageId, message.MessageId);
        _ = transaction.KeyExpireAsync(RedisKeys.ChatUserIndex(message.SenderUserNickName), RedisSettings.RedisCacheTtl);

        if (message.RoomId.HasValue)
        {
            _ = transaction.SortedSetAddAsync(RedisKeys.ChatRoomIndex(message.RoomId.Value), message.MessageId, message.MessageId);
            _ = transaction.KeyExpireAsync(RedisKeys.ChatRoomIndex(message.RoomId.Value), RedisSettings.RedisCacheTtl);
        }

        if (!string.IsNullOrWhiteSpace(message.TargetUserNickName))
        {
            _ = transaction.SortedSetAddAsync(RedisKeys.ChatTargetIndex(message.TargetUserNickName), message.MessageId, message.MessageId);
            _ = transaction.KeyExpireAsync(RedisKeys.ChatTargetIndex(message.TargetUserNickName), RedisSettings.RedisCacheTtl);
        }

        bool committed = await transaction.ExecuteAsync();
        if (!committed)
        {
            logger.LogWarning("Failed to set chat message cache for {MessageId}", message.MessageId);
        }
    }

    /// <summary>
    /// ChatMessage 캐시 삭제
    /// </summary>
    private async Task DeleteChatMessageCacheAsync(long messageId, string? senderName, long? roomId, string? targetName)
    {
        var transaction = _database.CreateTransaction();

        _ = transaction.KeyDeleteAsync(RedisKeys.ChatMessage(messageId));
        _ = transaction.SortedSetRemoveAsync(RedisKeys.ChatAllMessages(), messageId);

        if (!string.IsNullOrWhiteSpace(senderName))
            _ = transaction.SortedSetRemoveAsync(RedisKeys.ChatUserIndex(senderName), messageId);

        if (roomId.HasValue)
            _ = transaction.SortedSetRemoveAsync(RedisKeys.ChatRoomIndex(roomId.Value), messageId);

        if (!string.IsNullOrWhiteSpace(targetName))
            _ = transaction.SortedSetRemoveAsync(RedisKeys.ChatTargetIndex(targetName), messageId);

        bool committed = await transaction.ExecuteAsync();
        if (!committed)
        {
            logger.LogWarning("Failed to delete chat message cache for {MessageId}", messageId);
        }
    }

    /// <summary>
    /// messageId 배열로 ChatMessage 목록 일괄 조회 (Batch)
    /// </summary>
    private async Task<IEnumerable<ChatMessage>> FetchMessagesByIds(RedisValue[] messageIds)
    {
        // Batch: 여러 Redis 명령을 한 번에 파이프라인으로 전송
        // N번 개별 호출 → 네트워크 왕복 N번
        // Batch → 네트워크 왕복 1번
        var batch = _database.CreateBatch();

        var fetchTasks = messageIds
            .Select(id => (
                Id: id,
                Task: batch.HashGetAllAsync(RedisKeys.ChatMessage((long)id))
            ))
            .ToList();

        batch.Execute();

        var messages = new List<ChatMessage>();
        foreach (var (id, task) in fetchTasks)
        {
            var entries = await task;
            if (entries.Length == 0) continue;

            if (!long.TryParse(id.ToString(), out var messageId)) continue;

            var message = ParseChatMessage(messageId, entries);
            if (message is not null)
                messages.Add(message);
        }

        return messages;
    }

    /// <summary>
    /// Redis Hash → ChatMessage 변환
    /// </summary>
    private ChatMessage? ParseChatMessage(long messageId, HashEntry[] entries)
    {
        var dict = entries.ToDictionary(
            x => x.Name.ToString(),
            x => x.Value.ToString());

        if (!dict.TryGetValue("SenderName", out var senderName) ||
            !dict.TryGetValue("ChatType", out var chatTypeStr) ||
            !dict.TryGetValue("Message", out var message) ||
            !dict.TryGetValue("SentAt", out var sentAtStr))
        {
            logger.LogWarning("Chat message {MessageId} has missing fields", messageId);
            return null;
        }

        if (!Enum.TryParse<ChatType>(chatTypeStr, out var chatType))
        {
            logger.LogWarning("Invalid chat type {ChatType}", chatTypeStr);
            return null;
        }

        if (!DateTime.TryParse(sentAtStr, null, DateTimeStyles.RoundtripKind, out var sentAt))
        {
            logger.LogWarning("Invalid chat message SentAt value {SentAt}", sentAtStr);
            return null;
        }

        long? roomId = null;
        if (chatType == ChatType.Room && dict.TryGetValue("RoomId", out var roomIdStr))
        {
            if (!long.TryParse(roomIdStr, out var rid))
            {
                logger.LogWarning("Invalid chat message room id for message {MessageId}", messageId);
                return null;
            }
            roomId = rid;
        }

        string? targetUserNickName = null;
        if (chatType == ChatType.Whisper && dict.TryGetValue("TargetUserNickName", out var targetStr))
        {
            targetUserNickName = targetStr;
        }

        return ChatMessage.FromRedis(messageId, senderName, chatType, message, sentAt, roomId, targetUserNickName);
    }
}
