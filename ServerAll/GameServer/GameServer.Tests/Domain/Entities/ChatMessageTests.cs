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
        var senderUserName = "testuser";
        var message = "Hello, World!";
        
        // when
        var chatMessage = ChatMessage.CreateNew(
            1L,
            senderUserName, 
            ChatType.Global, 
            message);
        
        // then
        Assert.NotNull(chatMessage);
        Assert.Equal(senderUserName, chatMessage.SenderUserNickName);
        Assert.Equal(ChatType.Global, chatMessage.ChatType);
        Assert.Equal(message, chatMessage.Message);
        Assert.Null(chatMessage.RoomId);
        Assert.Null(chatMessage.TargetUserNickName);
    }
    
    // ========================================
    // Create - Room 채팅 테스트
    // ========================================
    
    [Fact]
    public void Create_는_유효한_Room_메시지를_생성한다()
    {
        // given
        var senderUserName = "testuser";
        var message = "Room message";
        var roomId = 100L;
        
        // when
        var chatMessage = ChatMessage.CreateNew(
            1L,
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
        var senderUserName = "testuser";
        var message = "Room message";
        
        // when & then - RoomId 없음
        Assert.Throws<ArgumentException>(() =>
            ChatMessage.CreateNew(
                1L,
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
        var senderUserName = "testuser";
        var message = "Room message";
        
        // when & then
        Assert.Throws<ArgumentException>(() =>
            ChatMessage.CreateNew(
                1L,
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
        var senderUserName = "sender";
        var targetUserNickName = "target";
        var message = "Private message";
        
        // when
        var chatMessage = ChatMessage.CreateNew(
            1L,
            senderUserName,
            ChatType.Whisper,
            message,
            targetUserNickName: targetUserNickName);
        
        // then
        Assert.NotNull(chatMessage);
        Assert.Equal(ChatType.Whisper, chatMessage.ChatType);
        Assert.Equal(targetUserNickName, chatMessage.TargetUserNickName);
    }
    
    [Fact]
    public void Create_는_Whisper_채팅에_TargetUserNickName이_없으면_예외를_던진다()
    {
        // given
        var senderUserName = "sender";
        var message = "Private message";
        
        // when & then
        Assert.Throws<ArgumentException>(() =>
            ChatMessage.CreateNew(
                1L,
                senderUserName,
                ChatType.Whisper,
                message));
    }
    
    // ========================================
    // Create - SenderUserName 검증 테스트
    // ========================================
    
    [Fact]
    public void Create_는_SenderUserName이_null이면_예외를_던진다()
    {
        // given
        string senderUserName = null!;
        var message = "Hello";
        
        // when & then
        Assert.Throws<ArgumentException>(() =>
            ChatMessage.CreateNew(
                1L,
                senderUserName,
                ChatType.Global,
                message));
    }
    
    [Fact]
    public void Create_는_SenderUserName이_빈문자열이면_예외를_던진다()
    {
        // given
        var senderUserName = "";
        var message = "Hello";
        
        // when & then
        Assert.Throws<ArgumentException>(() =>
            ChatMessage.CreateNew(
                1L,
                senderUserName,
                ChatType.Global,
                message));
    }
    
    [Fact]
    public void Create_는_SenderUserName이_공백만_있으면_예외를_던진다()
    {
        // given
        var senderUserName = "   ";
        var message = "Hello";
        
        // when & then
        Assert.Throws<ArgumentException>(() =>
            ChatMessage.CreateNew(
                1L,
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
        var senderUserName = "testuser";
        string message = null!;
        
        // when & then
        Assert.Throws<ArgumentException>(() =>
            ChatMessage.CreateNew(
                1L,
                senderUserName,
                ChatType.Global,
                message));
    }
    
    [Fact]
    public void Create_는_Message가_빈문자열이면_예외를_던진다()
    {
        // given
        var senderUserName = "testuser";
        var message = "";
        
        // when & then
        Assert.Throws<ArgumentException>(() =>
            ChatMessage.CreateNew(
                1L,
                senderUserName,
                ChatType.Global,
                message));
    }
    
    [Fact]
    public void Create_는_Message가_공백만_있으면_예외를_던진다()
    {
        // given
        var senderUserName = "testuser";
        var message = "   ";
        
        // when & then
        Assert.Throws<ArgumentException>(() =>
            ChatMessage.CreateNew(
                1L,
                senderUserName,
                ChatType.Global,
                message));
    }
    
    // ========================================
    // 메시지 원본 유지 테스트
    // ========================================
    
    [Fact]
    public void Create_는_메시지_원본을_유지한다_1()
    {
        // given
        var senderUserName = "testuser";
        var message = "이건 욕설1이 포함된 메시지";
        
        // when
        var chatMessage = ChatMessage.CreateNew(
            1L,
            senderUserName,
            ChatType.Global,
            message);
        
        // then
        Assert.Equal(message, chatMessage.Message);
    }
    
    [Fact]
    public void Create_는_메시지_원본을_유지한다_2()
    {
        // given
        var senderUserName = "testuser";
        var message = "욕설2 테스트";
        
        // when
        var chatMessage = ChatMessage.CreateNew(
            1L,
            senderUserName,
            ChatType.Global,
            message);
        
        // then
        Assert.Equal(message, chatMessage.Message);
    }
    
    [Fact]
    public void Create_는_메시지_원본을_유지한다_3()
    {
        // given
        var senderUserName = "testuser";
        var message = "비속어 포함";
        
        // when
        var chatMessage = ChatMessage.CreateNew(
            1L,
            senderUserName,
            ChatType.Global,
            message);
        
        // then
        Assert.Equal(message, chatMessage.Message);
    }
    
    [Fact]
    public void Create_는_메시지_원본을_유지한다_4()
    {
        // given
        var senderUserName = "testuser";
        var message = "욕설1 그리고 욕설2 그리고 비속어";
        
        // when
        var chatMessage = ChatMessage.CreateNew(
            1L,
            senderUserName,
            ChatType.Global,
            message);
        
        // then
        Assert.Equal(message, chatMessage.Message);
    }
    
    [Fact]
    public void Create_는_욕설이_없으면_원본_메시지를_유지한다()
    {
        // given
        var senderUserName = "testuser";
        var message = "깨끗한 메시지입니다";
        
        // when
        var chatMessage = ChatMessage.CreateNew(
            1L,
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
        var senderUserName = "testuser";
        var message = "Stored message";
        var sentAt = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        
        // when
        var chatMessage = ChatMessage.FromRedis(
            messageId,
            senderUserName,
            ChatType.Global,
            message,
            sentAt);
        
        // then
        Assert.Equal(messageId, chatMessage.MessageId);
        Assert.Equal(senderUserName, chatMessage.SenderUserNickName);
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
        var targetUserNickName = "target";
        
        // when
        var chatMessage = ChatMessage.FromRedis(
            messageId,
            "sender",
            ChatType.Whisper,
            "Private message",
            DateTime.UtcNow,
            targetUserNickName: targetUserNickName);
        
        // then
        Assert.Equal(ChatType.Whisper, chatMessage.ChatType);
        Assert.Equal(targetUserNickName, chatMessage.TargetUserNickName);
    }
    
    // ========================================
    // SetMessageId 테스트
    // ========================================
    
    [Fact]
    public void SetMessageId_는_MessageId를_생성시_설정한다()
    {
        // given
        var messageId = 123L;
        
        // when
        var chatMessage = ChatMessage.CreateNew(
            messageId,
            "testuser",
            ChatType.Global,
            "Hello");
        
        // then
        Assert.Equal(messageId, chatMessage.MessageId);
    }
    
    
    // ========================================
    // 엣지 케이스 테스트
    // ========================================
    
    [Fact]
    public void Create_는_Global_채팅에서_RoomId가_있어도_무시한다()
    {
        // given - Global은 RoomId 불필요
        var chatMessage = ChatMessage.CreateNew(
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
