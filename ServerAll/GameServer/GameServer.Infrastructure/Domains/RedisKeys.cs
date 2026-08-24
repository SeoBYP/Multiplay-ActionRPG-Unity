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

    // Inventory (소유 아이템 — Hash: field=itemId, value=quantity)
    public static string UserInventory(long userId) => $"{Prefix}:user:inventory:{userId}";

    // Equipment (착용 상태 — Hash: field=(int)slot, value=itemId)
    public static string UserEquipment(long userId) => $"{Prefix}:user:equipment:{userId}";

    // Wallet (재화/골드 잔액 — String: 정수 잔액)
    public static string UserWallet(long userId) => $"{Prefix}:user:wallet:{userId}";

    // UserCredential
    public static string UserCredential(long userId) => $"{Prefix}:user:credential:{userId}";
    public static string UserCredentialEmailMapping(string email) => $"{Prefix}:user:credential:email:{email}";
    // 직전 세대 리프레시 토큰 해시 (재사용 탐지 — DB 아닌 Redis 전용 휘발성)
    public static string UserRefreshTokenPrevious(long userId) => $"{Prefix}:user:credential:refresh:prev:{userId}";
    
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

    // 방 준비 상태(Set of userId). DB 컬럼이 아니라 Redis 전용 — 방과 수명을 같이하는 휘발성 로비 상태다.
    // 유실되면 전원 미준비로 되돌아갈 뿐이라 게임 진행에 영구 손상이 없다.
    public static string DungeonRoomReady(long roomId) => $"{Prefix}:room:ready:{roomId}";
    
    // GameSession
    public static string GameSession(long gameSessionId) => $"{Prefix}:gamesession:{gameSessionId}";
    public static string GameSessionByRoom(long roomId) => $"{Prefix}:gamesession:by-room:{roomId}";
    
    // GameSessionPlayer
    public static string GameSessionPlayer(long gameSessionId, long userId) => $"{Prefix}:session:player:{gameSessionId}:{userId}";
    public static string GameSessionPlayerBySession(long gameSessionId) => $"{Prefix}:session:player:by-session:{gameSessionId}";
    public static string GameSessionPlayerByUser(long userId) => $"{Prefix}:session:player:by-user:{userId}";
    
    // 보상 지급 멱등은 Redis 가 아니라 DB 원장(reward_grants, GrantKey UNIQUE)이 담당한다.
    // Redis 키로 잠그면 키와 지급이 서로 다른 저장소라 "지급됐는데 기록이 없다 / 그 반대" 창이 남고,
    // claim-first 는 지급 도중 실패 시 재시도까지 막아 영구 미지급이 됐다.

    // Chat
    public static string ChatMessage(long messageId) => $"{Prefix}:chat:message:{messageId}";
    public static string ChatAllMessages() => $"{Prefix}:chat:message:all";
    public static string ChatUserIndex(string userName) => $"{Prefix}:chat:message:user:{userName}";
    public static string ChatRoomIndex(long roomId) => $"{Prefix}:chat:message:room:{roomId}";
    public static string ChatTargetIndex(string userName) => $"{Prefix}:chat:message:target:{userName}";
}
