namespace GameServer.Infrastructure.Domains;

public static class RedisKeys
{
    private const string Prefix = "game";

    // User
    public static string User(long userId) => $"{Prefix}:user:{userId}";
    public static string UserPublicIdMapping(string publicId) => $"{Prefix}:user:publicid:{publicId}";
    
    // UserProfile
    public static string UserProfile(long userId) => $"{Prefix}:user:profile:{userId}";

    // UserProgression (레벨·경험치)
    public static string UserProgression(long userId) => $"{Prefix}:user:progression:{userId}";
    
    // UserCredential
    public static string UserCredential(long userId) => $"{Prefix}:user:credential:{userId}";
    public static string UserCredentialEmailMapping(string email) => $"{Prefix}:user:credential:email:{email}";
    
    // UserSession
    public static string UserSession(string sessionId) => $"{Prefix}:session:{sessionId}";
    public static string UserSessionActive() => $"{Prefix}:session:active";
    public static string UserSessionMapping(long userId) => $"{Prefix}:user:session:{userId}";
    
    // DungeonRoom
    public static string DungeonRoom(long roomId) => $"{Prefix}:room:{roomId}";
    public static string DungeonRoomActive() => $"{Prefix}:room:active";
    
    // DungeonRoomPlayer
    public static string DungeonRoomPlayer(long roomId, long userId) => $"{Prefix}:room:player:{roomId}:{userId}";
    public static string DungeonRoomPlayerByRoom(long roomId) => $"{Prefix}:room:player:by-room:{roomId}";
    public static string DungeonRoomPlayerByUser(long userId) => $"{Prefix}:room:player:by-user:{userId}";
    
    // GameSession
    public static string GameSession(long gameSessionId) => $"{Prefix}:gamesession:{gameSessionId}";
    public static string GameSessionByRoom(long roomId) => $"{Prefix}:gamesession:by-room:{roomId}";
    
    // GameSessionPlayer
    public static string GameSessionPlayer(long gameSessionId, long userId) => $"{Prefix}:session:player:{gameSessionId}:{userId}";
    public static string GameSessionPlayerBySession(long gameSessionId) => $"{Prefix}:session:player:by-session:{gameSessionId}";
    public static string GameSessionPlayerByUser(long userId) => $"{Prefix}:session:player:by-user:{userId}";
    
    // DungeonResult (보상 멱등 — 처리완료 RoomId 집합)
    public static string DungeonResultProcessed() => $"{Prefix}:dungeon:result:done";

    // Chat
    public static string ChatMessage(long messageId) => $"{Prefix}:chat:message:{messageId}";
    public static string ChatAllMessages() => $"{Prefix}:chat:message:all";
    public static string ChatUserIndex(string userName) => $"{Prefix}:chat:message:user:{userName}";
    public static string ChatRoomIndex(long roomId) => $"{Prefix}:chat:message:room:{roomId}";
    public static string ChatTargetIndex(string userName) => $"{Prefix}:chat:message:target:{userName}";
}
