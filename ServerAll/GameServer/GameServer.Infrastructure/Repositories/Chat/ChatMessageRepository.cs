using System.Globalization;
using GameServer.Domain.Entities.Chat;
using GameServer.Infrastructure.Interfaces.Chat;
using StackExchange.Redis;

namespace GameServer.Infrastructure.Repositories.Chat;

public class ChatMessageRepository(IConnectionMultiplexer connectionMultiplexer) : IChatMessageRepository
{
    private readonly IDatabase _database = connectionMultiplexer.GetDatabase();
    
    private const string MessageHashKey = "game:chat:message:{0}";          // 메시지 본문
    private const string MessageCounterKey = "game:chat:message:id:counter"; // ID 카운터
    private const string AllMessagesKey = "game:chat:message:all";           // 전체 인덱스
    private const string UserIndexKey = "game:chat:message:user:{0}";        // 유저별 인덱스 (닉네임 기준)
    private const string RoomIndexKey = "game:chat:message:room:{0}";        // 방별 인덱스
    private const string TargetIndexKey = "game:chat:message:target:{0}";    // 귓속말 대상 인덱스 (닉네임 기준)
    
    public async Task<ChatMessage> CreateAsync(
        string senderName,
        ChatType chatType,
        string message,
        long? roomId,
        string? targetUserNickName)
    {
        // 1. Domain Entity 생성 (검증 + 욕설 필터링)
        var chatMessage = ChatMessage.Create(senderName, chatType, message, roomId, targetUserNickName);
        
        var messageId = await _database.StringIncrementAsync(MessageCounterKey);
        chatMessage.SetMessageId(messageId);

        // 3. Redis 트랜잭션으로 원자적 저장
        var transaction = _database.CreateTransaction();
        var tasks = new List<Task>();

        // 3-1. 메시지 본문 저장 (Hash)
        var hashFields = new List<HashEntry>
        {
            new("MessageId", messageId),
            new("SenderName", senderName),
            new("ChatType", chatType.ToString()),
            new("Message", chatMessage.Message), // 욕설 필터링된 메시지 저장
            new("SentAt", chatMessage.SentAt.ToString("O")),
        };

        if (chatType == ChatType.Room)
        {
            if (!roomId.HasValue)
                throw new ArgumentException("RoomId is required for room chat", nameof(roomId));
            hashFields.Add(new HashEntry("RoomId", roomId.Value));
        }

        if (chatType == ChatType.Whisper)
        {
            if (string.IsNullOrWhiteSpace(targetUserNickName))
                throw new ArgumentException("TargetUserNickName is required for whisper", nameof(targetUserNickName));
            hashFields.Add(new HashEntry("TargetUserNickName", targetUserNickName));
        }

        tasks.Add(transaction.HashSetAsync(
            string.Format(MessageHashKey, messageId),
            hashFields.ToArray()));
        
        // 전체 인덱스 - [수정] CreateAsync에서 빠져있던 전체 인덱스 추가
        tasks.Add(transaction.SortedSetAddAsync(AllMessagesKey, messageId, messageId));

        // 유저 인덱스
        tasks.Add(transaction.SortedSetAddAsync(
            string.Format(UserIndexKey, senderName), messageId, messageId));

        // 방 인덱스 (Room 채팅일 때만)
        if (chatType == ChatType.Room && roomId.HasValue)
        {
            tasks.Add(transaction.SortedSetAddAsync(
                string.Format(RoomIndexKey, roomId.Value), messageId, messageId));
        }

        // 귓속말 대상 인덱스 (Whisper일 때만)
        if (chatType == ChatType.Whisper && !string.IsNullOrWhiteSpace(targetUserNickName))
        {
            tasks.Add(transaction.SortedSetAddAsync(
                string.Format(TargetIndexKey, targetUserNickName), messageId, messageId));
        }

        // 4. 트랜잭션 실행
        bool committed = await transaction.ExecuteAsync();
        if (!committed)
            throw new InvalidOperationException("Failed to create chat message: transaction rolled back");

        await Task.WhenAll(tasks);
        return chatMessage;
    }
    
    public async Task<ChatMessage?> GetMessageByIdAsync(long messageId)
    {
        var entries = await _database.HashGetAllAsync(string.Format(MessageHashKey, messageId));
        if (entries.Length == 0)
            return null;

        return ParseChatMessage(messageId, entries);
    }

    
    public async Task<IEnumerable<ChatMessage>> GetAllMessagesAsync()
    {
        var messageIds = await _database.SortedSetRangeByRankAsync(
            AllMessagesKey,
            order: Order.Descending);

        if (messageIds.Length == 0)
            return Enumerable.Empty<ChatMessage>();

        return await FetchMessagesByIds(messageIds);
    }
    
    public async Task<IEnumerable<ChatMessage>> GetMessagesByUserNameAsync(
        string userName, int limit, long? beforeMessageId)
    {
        
        var maxScore = beforeMessageId.HasValue
            ? (double)beforeMessageId.Value - 1  // beforeMessageId 미만
            : double.MaxValue;                    // 처음 조회 시 최신부터

        var messageIds = await _database.SortedSetRangeByScoreAsync(
            string.Format(UserIndexKey, userName),
            stop: maxScore,
            take: limit,
            order: Order.Descending); // 최신 메시지부터

        if (messageIds.Length == 0)
            return Enumerable.Empty<ChatMessage>();

        return await FetchMessagesByIds(messageIds);
    }
    
    public async Task<IEnumerable<ChatMessage>> GetMessagesByRoomIdAsync(
        long roomId, int limit, long? beforeMessageId)
    {
        // [수정] GetMessagesByUserIdAsync와 동일한 패턴으로 수정
        var maxScore = beforeMessageId.HasValue
            ? (double)beforeMessageId.Value - 1
            : double.MaxValue;

        var messageIds = await _database.SortedSetRangeByScoreAsync(
            string.Format(RoomIndexKey, roomId),
            stop: maxScore,
            take: limit,
            order: Order.Descending);

        if (messageIds.Length == 0)
            return Enumerable.Empty<ChatMessage>();

        return await FetchMessagesByIds(messageIds);
    }

    public async Task<bool> DeleteAsync(long messageId)
    {
        var message = await GetMessageByIdAsync(messageId);
        if (message is null)
            return false;

        var transaction = _database.CreateTransaction();
        var tasks = new List<Task>();

        // 본문 삭제
        tasks.Add(transaction.KeyDeleteAsync(string.Format(MessageHashKey, messageId)));

        // 인덱스 삭제 - [수정] SortedSetRemoveAsync 사용
        tasks.Add(transaction.SortedSetRemoveAsync(AllMessagesKey, messageId));
        tasks.Add(transaction.SortedSetRemoveAsync(
            string.Format(UserIndexKey, message.SenderUserName), messageId));

        if (message.RoomId.HasValue)
        {
            tasks.Add(transaction.SortedSetRemoveAsync(
                string.Format(RoomIndexKey, message.RoomId.Value), messageId));
        }

        if (!string.IsNullOrWhiteSpace(message.TargetUserNickName))
        {
            tasks.Add(transaction.SortedSetRemoveAsync(
                string.Format(TargetIndexKey, message.TargetUserNickName), messageId));
        }

        return await ExecuteTransactionAsync(transaction, tasks);
    }
    
    public async Task<bool> DeleteAllAsync()
    {
        var server = connectionMultiplexer.GetServer(connectionMultiplexer.GetEndPoints().First());

        var keysToDelete = new List<RedisKey> { (RedisKey)MessageCounterKey };
        keysToDelete.AddRange(server.Keys(pattern: "game:chat:message:*").ToArray());

        if (keysToDelete.Count == 0)
            return true;

        var transaction = _database.CreateTransaction();
        var tasks = keysToDelete.Distinct()
            .Select(key => transaction.KeyDeleteAsync(key))
            .Cast<Task>()
            .ToList();

        return await ExecuteTransactionAsync(transaction, tasks);
    }
    
    public async Task<bool> DeleteByUserNameAsync(string userName)
    {
        var userKey = string.Format(UserIndexKey, userName);

        // [수정] SortedSetRangeByRankAsync 사용
        var messageIds = await _database.SortedSetRangeByRankAsync(userKey);
        if (messageIds.Length == 0)
            return true;

        var transaction = _database.CreateTransaction();
        var tasks = new List<Task>();

        foreach (var idValue in messageIds)
        {
            if (!long.TryParse(idValue.ToString(), out var messageId))
                continue;

            var message = await GetMessageByIdAsync(messageId);
            if (message is null) continue;

            tasks.Add(transaction.KeyDeleteAsync(string.Format(MessageHashKey, messageId)));
            tasks.Add(transaction.SortedSetRemoveAsync(AllMessagesKey, messageId));

            if (message.RoomId.HasValue)
                tasks.Add(transaction.SortedSetRemoveAsync(
                    string.Format(RoomIndexKey, message.RoomId.Value), (RedisValue)messageId));

            if (!string.IsNullOrWhiteSpace(message.TargetUserNickName))
                tasks.Add(transaction.SortedSetRemoveAsync(
                    string.Format(TargetIndexKey, message.TargetUserNickName), (RedisValue)messageId));
        }

        tasks.Add(transaction.KeyDeleteAsync(userKey));

        return await ExecuteTransactionAsync(transaction, tasks);
    }
    
    public async Task<bool> DeleteByRoomIdAsync(long roomId)
    {
        var roomKey = string.Format(RoomIndexKey, roomId);

        var messageIds = await _database.SortedSetRangeByRankAsync(roomKey);
        if (messageIds.Length == 0)
            return true;

        var transaction = _database.CreateTransaction();
        var tasks = new List<Task>();

        foreach (var idValue in messageIds)
        {
            if (!long.TryParse(idValue.ToString(), out var messageId))
                continue;

            var message = await GetMessageByIdAsync(messageId);
            if (message is null) continue;

            tasks.Add(transaction.KeyDeleteAsync(string.Format(MessageHashKey, messageId)));
            tasks.Add(transaction.SortedSetRemoveAsync(AllMessagesKey, messageId));
            tasks.Add(transaction.SortedSetRemoveAsync(
                string.Format(UserIndexKey, message.SenderUserName), (RedisValue)messageId));

            if (!string.IsNullOrWhiteSpace(message.TargetUserNickName))
                tasks.Add(transaction.SortedSetRemoveAsync(
                    string.Format(TargetIndexKey, message.TargetUserNickName), (RedisValue)messageId));
        }

        tasks.Add(transaction.KeyDeleteAsync(roomKey));

        return await ExecuteTransactionAsync(transaction, tasks);
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
                Task: batch.HashGetAllAsync(string.Format(MessageHashKey, id))
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
            Console.WriteLine($"[ChatMessageRepository] Message {messageId} has missing fields");
            return null;
        }

        if (!Enum.TryParse<ChatType>(chatTypeStr, out var chatType))
        {
            Console.WriteLine($"[ChatMessageRepository] Invalid ChatType: {chatTypeStr}");
            return null;
        }

        if (!DateTime.TryParse(sentAtStr, null, DateTimeStyles.RoundtripKind, out var sentAt))
        {
            Console.WriteLine($"[ChatMessageRepository] Invalid SentAt: {sentAtStr}");
            return null;
        }

        long? roomId = null;
        if (chatType == ChatType.Room && dict.TryGetValue("RoomId", out var roomIdStr))
        {
            if (!long.TryParse(roomIdStr, out var rid))
            {
                Console.WriteLine("[ChatMessageRepository] Invalid RoomId");
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

    /// <summary>
    /// 트랜잭션 실행 공통 메서드
    /// </summary>
    private static async Task<bool> ExecuteTransactionAsync(ITransaction transaction, List<Task> queuedTasks)
    {
        bool committed = await transaction.ExecuteAsync().ConfigureAwait(false);
        if (!committed)
            return false;

        await Task.WhenAll(queuedTasks).ConfigureAwait(false);
        return true;
    }
}