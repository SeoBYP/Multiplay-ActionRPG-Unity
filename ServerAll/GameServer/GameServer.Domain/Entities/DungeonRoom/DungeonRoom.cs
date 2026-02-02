namespace GameServer.Domain.Entities;

/// <summary>
/// 던전 방의 현재 상태를 나타내는 열거형
/// </summary>
public enum RoomStatus
{
    /// <summary>
    /// 대기 중 - 플레이어 입장 가능
    /// </summary>
    Waiting,   
    /// <summary>
    /// 게임 중 - 플레이어 입장 불가
    /// </summary>
    Playing,  
    /// <summary>
    /// 종료됨 - 모든 플레이어가 나감
    /// </summary>
    Closed    
}

public class DungeonRoom
{
    /// <summary>방 고유 식별자 (Redis에서 생성)</summary>
    public long RoomId { get; private set; }
    
    /// <summary>방 이름 (플레이어가 지정)</summary>
    public string RoomName { get; private set; } = string.Empty;
    
    /// <summary>방장 UserId (SessionId가 아닌 UserId 사용 - 재로그인 시에도 유지)</summary>
    public long HostUserId { get; private set; }
    
    /// <summary>최대 플레이어 수 (기본값 4명)</summary>
    public int MaxPlayers { get; private set; }
    
    /// <summary>현재 방에 있는 플레이어 UserId 목록 (HashSet으로 중복 방지)</summary>
    public HashSet<long> CurrentPlayers { get; private set; }
    
    /// <summary>방 상태 (대기/게임중/종료)</summary>
    public RoomStatus Status { get; private set; }
    
    /// <summary>방 생성 시각 (UTC)</summary>
    public DateTime CreatedAt { get; private set; }
    
    
    private DungeonRoom(){ }
    
    
    /// <summary>
    /// 새로운 던전 방을 생성합니다 (Factory Method 패턴)
    /// </summary>
    public static DungeonRoom Create(string roomName, long hostUserId, int maxPlayers)
    {
        // 방 이름 검증: null, 빈 문자열, 공백만 있는 경우 예외
        if(string.IsNullOrWhiteSpace(roomName))
            throw new ArgumentException("Room name cannot be null or whitespace", nameof(roomName));
        
        // 방장 UserId 검증: 0 이하는 유효하지 않은 ID
        if(hostUserId <= 0)
            throw new ArgumentException("Host ID cannot be less than or equal to zero", nameof(hostUserId));
        
        // 최대 플레이어 수 검증: 최소 2명 이상
        if(maxPlayers < 2)
            throw new ArgumentException("Max players cannot be less than 2", nameof(maxPlayers));
        
        // 유효성 검증을 모두 통과한 경우, 새 DungeonRoom 인스턴스 생성
        return new DungeonRoom
        {
            RoomName = roomName,
            HostUserId = hostUserId,
            MaxPlayers = maxPlayers,
            Status = RoomStatus.Waiting, // 생성 직후는 항상 대기 상태
            CurrentPlayers = new HashSet<long> { hostUserId }, // 방장을 첫 플레이어로 자동 추가
            CreatedAt = DateTime.UtcNow // UTC 시간으로 저장 (서버 위치와 무관하게 통일)
        };
    }

    /// <summary>
    /// Redis에서 조회한 데이터로 DungeonRoom 재구성 (Repository 전용)
    /// </summary>
    public static DungeonRoom FromRedis(
        long roomId,
        string roomName,
        long hostUserId,
        int maxPlayers,
        RoomStatus status,
        HashSet<long> currentPlayers,
        DateTime createdAt)
    {
        // 검증 없이 바로 생성 (Redis에서 가져온 데이터는 이미 검증됨)
        return new DungeonRoom
        {
            RoomId = roomId,
            RoomName = roomName,
            HostUserId = hostUserId,
            MaxPlayers = maxPlayers,
            Status = status,
            CurrentPlayers = currentPlayers,
            CreatedAt = createdAt
        };
    }

    public static DungeonRoom FromRoomInfoDto(long roomId, string roomName, long hostUserId, int maxPlayers, RoomStatus status)
    {
        return new DungeonRoom
        {
            RoomId = roomId,
            RoomName = roomName,
            HostUserId = hostUserId,
            MaxPlayers = maxPlayers,
            Status = status
        };
    }
    
    // ========== 인프라 계층 전용 메서드 ==========
    
    /// <summary>
    /// RoomId 설정 (Repository에서만 호출)
    /// internal: 같은 어셈블리(프로젝트)에서만 접근 가능
    /// Redis에 저장 시 INCR로 생성한 ID를 할당하기 위한 메서드
    /// </summary>
    public void SetRoomId(long roomId)
    {
        RoomId = roomId; // 검증 없이 바로 할당 (Repository가 책임짐)
    }
    
    
    /// <summary>
    /// 플레이어가 방에 입장합니다
    /// </summary>
    public void Join(long userId)
    {
        // 검증 1: UserId는 1 이상이어야 함
        if(userId <= 0)
            throw new ArgumentException("User ID cannot be less than or equal to zero", nameof(userId));
        
        // 검증 2: 이미 방에 있는 플레이어는 중복 입장 불가
        if(IsExist(userId))
            throw new InvalidOperationException("User is already in the room");
        
        // 검증 3: 방이 가득 찬 경우 입장 불가
        if(IsFull)
            throw new InvalidOperationException("Room is full");
        
        // 검증 4: 게임이 시작된 방에는 입장 불가 (대기 중일 때만 입장 가능)
        if(Status != RoomStatus.Waiting)
            throw new InvalidOperationException("Room is not waiting"); 
        
        // 모든 검증을 통과하면 플레이어 추가
        CurrentPlayers.Add(userId);
    }
    
    /// <summary>
    /// 플레이어가 방에서 퇴장합니다
    /// </summary>
    public void Leave(long userId)
    {
        // 검증 1: UserId는 1 이상이어야 함
        if(userId <= 0)
            throw new ArgumentException("User ID cannot be less than or equal to zero", nameof(userId));
        
        // 검증 2: 방에 존재하지 않는 플레이어는 퇴장 불가
        if(!IsExist(userId))
            throw new InvalidOperationException("User is not in the room");

        // HashSet.Remove는 성공 시 true, 실패 시 false 반환
        var success = CurrentPlayers.Remove(userId);
        
        // 방장이 퇴장하고 && 방이 비지 않았고 && 방장이었다면
        // → 남은 플레이어 중 첫 번째 사람을 새 방장으로 임명 (방장 위임)
        if (success && !IsEmpty && IsHost(userId))
        {
            HostUserId = CurrentPlayers.First(); // HashSet의 첫 번째 요소를 방장으로
        }

        // 모든 플레이어가 퇴장하면 방 자동 종료
        if (IsEmpty)
        {
            Status = RoomStatus.Closed; // 더 이상 사용하지 않는 방으로 표시
        }
    }
    
    /// <summary>방이 가득 찼는지 확인 (현재 인원 >= 최대 인원)</summary>
    public bool IsFull => CurrentPlayers.Count >= MaxPlayers;
    
    /// <summary>방이 비어있는지 확인 (현재 인원 == 0)</summary>
    public bool IsEmpty => CurrentPlayers.Count == 0;
    
    /// <summary>해당 UserId가 방장인지 확인</summary>
    public bool IsHost(long userId) => HostUserId == userId;
    
    /// <summary>해당 UserId가 방에 있는지 확인</summary>
    public bool IsExist(long userId) => CurrentPlayers.Contains(userId);
    
    /// <summary>현재 플레이어 수 반환</summary>
    public int GetPlayerCount() => CurrentPlayers.Count;
    
    
    /// <summary>
    /// 방 설정을 변경합니다 (방장만 가능)
    /// </summary>
    public void UpdateRoomSettings(long userId, string? roomName = null, int? maxPlayers = null)
    {
        // 권한 검증
        if (!IsHost(userId))
            throw new UnauthorizedAccessException("Only the host can update room settings");
    
        // 상태 검증
        if (Status == RoomStatus.Playing)
            throw new InvalidOperationException("Cannot update room settings while playing");
    
        // 방 이름 변경
        if (roomName != null)
        {
            if (string.IsNullOrWhiteSpace(roomName))
                throw new ArgumentException("Room name cannot be empty");
        
            RoomName = roomName;
        }
    
        // 최대 플레이어 수 변경
        if (maxPlayers.HasValue)
        {
            if (maxPlayers.Value < 2)
                throw new ArgumentException("Max players cannot be less than 2");
        
            if (maxPlayers.Value < CurrentPlayers.Count)
                throw new InvalidOperationException(
                    $"Cannot reduce max players to {maxPlayers.Value}. Current players: {CurrentPlayers.Count}");
        
            MaxPlayers = maxPlayers.Value;
        }
    }
    
    /// <summary>
    /// 게임을 시작합니다 (방장만 가능)
    /// </summary>
    public void StartGame(long userId)
    {
        // 권한 검증: 방장만 게임 시작 가능
        if(!IsHost(userId))
            throw new UnauthorizedAccessException("User is not host");
        
        // 상태 검증 1: 최소 플레이어 수 검증
        if(CurrentPlayers.Count < 2)
            throw new InvalidOperationException("need at least 2 players to start the game");
        
        // 상태 검증 2: 이미 종료된 방에서는 게임 시작 불가
        if(Status == RoomStatus.Closed)
            throw new InvalidOperationException("Room is already closed");
        
        // 상태 검증 3: 이미 게임이 진행 중이면 중복 시작 불가
        if(Status == RoomStatus.Playing)
            throw new InvalidOperationException("Room is already playing");
        
        // 모든 검증을 통과하면 게임 시작 (상태 변경)
        Status = RoomStatus.Playing;
    }


}