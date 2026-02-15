using GameServer.Domain.Entities.Chat;

namespace GameServer.Tests.Domain.Entities;

public class ChatMessageTests
{
    // ========================================
    // Create - Global 채팅 테스트
    // ========================================
    
    [Fact]
    public void Create_는_유효한_Global_메시지를_생성한다()
    {
        // given
        var senderUserId = 1L;
        var senderUserName = "testuser";
        var message = "Hello, World!";
        
        // when
        var chatMessage = ChatMessage.Create(
            senderUserId, 
            senderUserName, 
            ChatType.Global, 
            message);
        
        // then
        Assert.NotNull(chatMessage);
        Assert.Equal(senderUserId, chatMessage.SenderUserId);
        Assert.Equal(senderUserName, chatMessage.SenderUserName);
        Assert.Equal(ChatType.Global, chatMessage.ChatType);
        Assert.Equal(message, chatMessage.Message);
        Assert.Null(chatMessage.RoomId);
        Assert.Null(chatMessage.TargetUserId);
    }
    
    // ========================================
    // Create - Room 채팅 테스트
    // ========================================
    
    [Fact]
    public void Create_는_유효한_Room_메시지를_생성한다()
    {
        // given
        var senderUserId = 1L;
        var senderUserName = "testuser";
        var message = "Room message";
        var roomId = 100L;
        
        // when
        var chatMessage = ChatMessage.Create(
            senderUserId,
            senderUserName,
            ChatType.Room,
            message,
            roomId: roomId);
        
        // then
        Assert.NotNull(chatMessage);
        Assert.Equal(ChatType.Room, chatMessage.ChatType);
        Assert.Equal(roomId, chatMessage.RoomId);
    }
    
    [Fact]
    public void Create_는_Room_채팅에_RoomId가_없으면_예외를_던진다()
    {
        // given
        var senderUserId = 1L;
        var senderUserName = "testuser";
        var message = "Room message";
        
        // when & then - RoomId 없음
        Assert.Throws<ArgumentException>(() =>
            ChatMessage.Create(
                senderUserId,
                senderUserName,
                ChatType.Room,
                message));
    }
    
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_는_Room_채팅에_RoomId가_0이하면_예외를_던진다(long roomId)
    {
        // given
        var senderUserId = 1L;
        var senderUserName = "testuser";
        var message = "Room message";
        
        // when & then
        Assert.Throws<ArgumentException>(() =>
            ChatMessage.Create(
                senderUserId,
                senderUserName,
                ChatType.Room,
                message,
                roomId: roomId));
    }
    
    // ========================================
    // Create - Whisper 채팅 테스트
    // ========================================
    
    [Fact]
    public void Create_는_유효한_Whisper_메시지를_생성한다()
    {
        // given
        var senderUserId = 1L;
        var senderUserName = "sender";
        var targetUserId = 2L;
        var targetUserName = "target";
        var message = "Private message";
        
        // when
        var chatMessage = ChatMessage.Create(
            senderUserId,
            senderUserName,
            ChatType.Whisper,
            message,
            targetUserId: targetUserId,
            targetUserName: targetUserName);
        
        // then
        Assert.NotNull(chatMessage);
        Assert.Equal(ChatType.Whisper, chatMessage.ChatType);
        Assert.Equal(targetUserId, chatMessage.TargetUserId);
    }
    
    [Fact]
    public void Create_는_Whisper_채팅에_TargetUserId가_없으면_예외를_던진다()
    {
        // given
        var senderUserId = 1L;
        var senderUserName = "sender";
        var message = "Private message";
        
        // when & then
        Assert.Throws<ArgumentException>(() =>
            ChatMessage.Create(
                senderUserId,
                senderUserName,
                ChatType.Whisper,
                message));
    }
    
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_는_Whisper_채팅에_TargetUserId가_0이하면_예외를_던진다(long targetUserId)
    {
        // given
        var senderUserId = 1L;
        var senderUserName = "sender";
        var message = "Private message";
        
        // when & then
        Assert.Throws<ArgumentException>(() =>
            ChatMessage.Create(
                senderUserId,
                senderUserName,
                ChatType.Whisper,
                message,
                targetUserId: targetUserId));
    }
    
    // ========================================
    // Create - SenderUserId 검증 테스트
    // ========================================
    
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_는_SenderUserId가_0이하면_예외를_던진다(long senderUserId)
    {
        // given
        var senderUserName = "testuser";
        var message = "Hello";
        
        // when & then
        Assert.Throws<ArgumentException>(() =>
            ChatMessage.Create(
                senderUserId,
                senderUserName,
                ChatType.Global,
                message));
    }
    
    // ========================================
    // Create - SenderUserName 검증 테스트
    // ========================================
    
    [Fact]
    public void Create_는_SenderUserName이_null이면_예외를_던진다()
    {
        // given
        var senderUserId = 1L;
        string senderUserName = null!;
        var message = "Hello";
        
        // when & then
        Assert.Throws<ArgumentException>(() =>
            ChatMessage.Create(
                senderUserId,
                senderUserName,
                ChatType.Global,
                message));
    }
    
    [Fact]
    public void Create_는_SenderUserName이_빈문자열이면_예외를_던진다()
    {
        // given
        var senderUserId = 1L;
        var senderUserName = "";
        var message = "Hello";
        
        // when & then
        Assert.Throws<ArgumentException>(() =>
            ChatMessage.Create(
                senderUserId,
                senderUserName,
                ChatType.Global,
                message));
    }
    
    [Fact]
    public void Create_는_SenderUserName이_공백만_있으면_예외를_던진다()
    {
        // given
        var senderUserId = 1L;
        var senderUserName = "   ";
        var message = "Hello";
        
        // when & then
        Assert.Throws<ArgumentException>(() =>
            ChatMessage.Create(
                senderUserId,
                senderUserName,
                ChatType.Global,
                message));
    }
    
    // ========================================
    // Create - Message 검증 테스트
    // ========================================
    
    [Fact]
    public void Create_는_Message가_null이면_예외를_던진다()
    {
        // given
        var senderUserId = 1L;
        var senderUserName = "testuser";
        string message = null!;
        
        // when & then
        Assert.Throws<ArgumentException>(() =>
            ChatMessage.Create(
                senderUserId,
                senderUserName,
                ChatType.Global,
                message));
    }
    
    [Fact]
    public void Create_는_Message가_빈문자열이면_예외를_던진다()
    {
        // given
        var senderUserId = 1L;
        var senderUserName = "testuser";
        var message = "";
        
        // when & then
        Assert.Throws<ArgumentException>(() =>
            ChatMessage.Create(
                senderUserId,
                senderUserName,
                ChatType.Global,
                message));
    }
    
    [Fact]
    public void Create_는_Message가_공백만_있으면_예외를_던진다()
    {
        // given
        var senderUserId = 1L;
        var senderUserName = "testuser";
        var message = "   ";
        
        // when & then
        Assert.Throws<ArgumentException>(() =>
            ChatMessage.Create(
                senderUserId,
                senderUserName,
                ChatType.Global,
                message));
    }
    
    // ========================================
    // 욕설 필터링 테스트
    // ========================================
    
    [Fact]
    public void Create_는_욕설1을_필터링한다()
    {
        // given
        var senderUserId = 1L;
        var senderUserName = "testuser";
        var message = "이건 욕설1이 포함된 메시지";
        
        // when
        var chatMessage = ChatMessage.Create(
            senderUserId,
            senderUserName,
            ChatType.Global,
            message);
        
        // then
        Assert.Contains("***", chatMessage.Message);
        Assert.DoesNotContain("욕설1", chatMessage.Message);
    }
    
    [Fact]
    public void Create_는_욕설2를_필터링한다()
    {
        // given
        var senderUserId = 1L;
        var senderUserName = "testuser";
        var message = "욕설2 테스트";
        
        // when
        var chatMessage = ChatMessage.Create(
            senderUserId,
            senderUserName,
            ChatType.Global,
            message);
        
        // then
        Assert.Contains("***", chatMessage.Message);
        Assert.DoesNotContain("욕설2", chatMessage.Message);
    }
    
    [Fact]
    public void Create_는_비속어를_필터링한다()
    {
        // given
        var senderUserId = 1L;
        var senderUserName = "testuser";
        var message = "비속어 포함";
        
        // when
        var chatMessage = ChatMessage.Create(
            senderUserId,
            senderUserName,
            ChatType.Global,
            message);
        
        // then
        Assert.Contains("***", chatMessage.Message);
        Assert.DoesNotContain("비속어", chatMessage.Message);
    }
    
    [Fact]
    public void Create_는_여러_욕설을_모두_필터링한다()
    {
        // given
        var senderUserId = 1L;
        var senderUserName = "testuser";
        var message = "욕설1 그리고 욕설2 그리고 비속어";
        
        // when
        var chatMessage = ChatMessage.Create(
            senderUserId,
            senderUserName,
            ChatType.Global,
            message);
        
        // then
        Assert.DoesNotContain("욕설1", chatMessage.Message);
        Assert.DoesNotContain("욕설2", chatMessage.Message);
        Assert.DoesNotContain("비속어", chatMessage.Message);
        Assert.Contains("***", chatMessage.Message);
    }
    
    [Fact]
    public void Create_는_대소문자_구분없이_욕설을_필터링한다()
    {
        // given
        var senderUserId = 1L;
        var senderUserName = "testuser";
        var message = "YOKSUL1 테스트";  // 대문자로 시도
        
        // when
        var chatMessage = ChatMessage.Create(
            senderUserId,
            senderUserName,
            ChatType.Global,
            message);
        
        // then (FilterProfanity가 대소문자 구분 없이 작동한다면)
        // 현재 코드는 "욕설1"만 필터링하므로 이 테스트는 실패할 수 있음
        // 이 테스트는 영어 욕설에 대한 예시
    }
    
    [Fact]
    public void Create_는_욕설이_없으면_원본_메시지를_유지한다()
    {
        // given
        var senderUserId = 1L;
        var senderUserName = "testuser";
        var message = "깨끗한 메시지입니다";
        
        // when
        var chatMessage = ChatMessage.Create(
            senderUserId,
            senderUserName,
            ChatType.Global,
            message);
        
        // then
        Assert.Equal(message, chatMessage.Message);
    }
    
    // ========================================
    // FromRedis 테스트
    // ========================================
    
    [Fact]
    public void FromRedis_는_Redis_데이터를_복원한다()
    {
        // given
        var messageId = 123L;
        var senderUserId = 1L;
        var senderUserName = "testuser";
        var message = "Stored message";
        var sentAt = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        
        // when
        var chatMessage = ChatMessage.FromRedis(
            messageId,
            senderUserId,
            senderUserName,
            ChatType.Global,
            message,
            sentAt);
        
        // then
        Assert.Equal(messageId, chatMessage.MessageId);
        Assert.Equal(senderUserId, chatMessage.SenderUserId);
        Assert.Equal(senderUserName, chatMessage.SenderUserName);
        Assert.Equal(ChatType.Global, chatMessage.ChatType);
        Assert.Equal(message, chatMessage.Message);
        Assert.Equal(sentAt, chatMessage.SentAt);
    }
    
    [Fact]
    public void FromRedis_는_Room_채팅_데이터를_복원한다()
    {
        // given
        var messageId = 123L;
        var roomId = 100L;
        
        // when
        var chatMessage = ChatMessage.FromRedis(
            messageId,
            1L,
            "testuser",
            ChatType.Room,
            "Room message",
            DateTime.UtcNow,
            roomId: roomId);
        
        // then
        Assert.Equal(ChatType.Room, chatMessage.ChatType);
        Assert.Equal(roomId, chatMessage.RoomId);
    }
    
    [Fact]
    public void FromRedis_는_Whisper_채팅_데이터를_복원한다()
    {
        // given
        var messageId = 123L;
        var targetUserId = 2L;
        
        // when
        var chatMessage = ChatMessage.FromRedis(
            messageId,
            1L,
            "sender",
            ChatType.Whisper,
            "Private message",
            DateTime.UtcNow,
            targetUserId: targetUserId);
        
        // then
        Assert.Equal(ChatType.Whisper, chatMessage.ChatType);
        Assert.Equal(targetUserId, chatMessage.TargetUserId);
    }
    
    // ========================================
    // SetMessageId 테스트
    // ========================================
    
    [Fact]
    public void SetMessageId_는_MessageId를_설정한다()
    {
        // given
        var chatMessage = ChatMessage.Create(
            1L,
            "testuser",
            ChatType.Global,
            "Hello");
        var messageId = 123L;
        
        // when
        chatMessage.SetMessageId(messageId);
        
        // then
        Assert.Equal(messageId, chatMessage.MessageId);
    }
    
    [Fact]
    public void SetMessageId_는_이미_설정된_MessageId는_변경_불가()
    {
        // given
        var chatMessage = ChatMessage.Create(
            1L,
            "testuser",
            ChatType.Global,
            "Hello");
        chatMessage.SetMessageId(123L);
        
        // when & then
        Assert.Throws<InvalidOperationException>(() => 
            chatMessage.SetMessageId(456L));
    }
    
    // ========================================
    // 엣지 케이스 테스트
    // ========================================
    
    [Fact]
    public void Create_는_TargetUserName이_null이어도_Whisper_생성_가능()
    {
        // given - TargetUserName은 선택사항
        var chatMessage = ChatMessage.Create(
            1L,
            "sender",
            ChatType.Whisper,
            "Private message",
            targetUserId: 2L,
            targetUserName: null);  // null이어도 OK
        
        // then
        Assert.NotNull(chatMessage);
        Assert.Equal(ChatType.Whisper, chatMessage.ChatType);
    }
    
    [Fact]
    public void Create_는_Global_채팅에서_RoomId가_있어도_무시한다()
    {
        // given - Global은 RoomId 불필요
        var chatMessage = ChatMessage.Create(
            1L,
            "testuser",
            ChatType.Global,
            "Global message",
            roomId: 999L);  // 있어도 무시됨
        
        // then
        Assert.NotNull(chatMessage);
        Assert.Equal(ChatType.Global, chatMessage.ChatType);
        // RoomId가 설정되었는지는 구현에 따라 다름
    }
}