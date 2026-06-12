using GameServer.Application.Common;
using GameServer.Domain.Entities;

namespace GameServer.Application.Domains.DungeonLobby.Interfaces;

public interface IDungeonLobbyService
{
    /// <summary>
    /// 새로운 방을 생성합니다
    /// </summary>
    /// <param name="userId">방장의 UserId</param>
    /// <param name="roomName">방 이름</param>
    /// <param name="maxPlayers">최대 플레이어 수 (기본 4명)</param>
    /// <returns>생성된 방 정보</returns>
    Task<Result<DungeonRoom>> CreateDungeonRoomAsync(string sessionId, string roomName, int maxPlayers, CancellationToken ct = default);

    /// <summary>
    /// 활성 방 목록을 조회합니다
    /// </summary>
    /// <returns>활성 방 목록</returns>
    Task<Result<IEnumerable<DungeonRoom>>> GetActiveDungeonRoomsAsync(CancellationToken ct = default);

    /// <summary>
    /// 특정 방 정보를 조회합니다
    /// </summary>
    /// <param name="roomId">방 ID</param>
    /// <returns>방 정보</returns>
    Task<Result<DungeonRoom>> GetDungeonRoomAsync(long roomId, CancellationToken ct = default);

    /// <summary>
    /// 특정 방 정보를 갱신합니다.
    /// </summary>
    /// <param name="userId"></param>
    /// <returns></returns>
    Task<Result<DungeonRoom>> UpdateRoomSettingsAsync(string sessionId, long roomId,
        string? roomName = null, int? maxPlayers = null, CancellationToken ct = default);

    /// <summary>
    /// 방에 입장합니다
    /// </summary>
    /// <param name="userId">입장할 사용자 ID</param>
    /// <param name="roomId">입장할 방 ID</param>
    /// <returns>입장 결과</returns>
    Task<Result<DungeonRoom>> JoinRoomAsync(string sessionId, long roomId, CancellationToken ct = default);

    /// <summary>
    /// 방에서 퇴장합니다
    /// </summary>
    /// <param name="userId">퇴장할 사용자 ID</param>
    /// <param name="roomId">퇴장할 방 ID</param>
    /// <returns>퇴장 결과</returns>
    Task<Result<DungeonRoom>> LeaveRoomAsync(string sessionId, long roomId, CancellationToken ct = default);

    // ========== 게임 시작 ==========

    /// <summary>
    /// 게임을 시작합니다 (방장만 가능)
    /// </summary>
    /// <param name="userId">게임 시작을 요청한 사용자 ID</param>
    /// <param name="roomId">게임을 시작할 방 ID</param>
    /// <returns>게임 시작 결과</returns>
    // mapId: 비우면 기본 맵(MapIds.Default). 특정 맵 강제 시 지정(StartRoomRequest.map_id, E2E 등).
    Task<Result<DungeonRoom>> StartGameAsync(string sessionId, long roomId, string traceId, string mapId = "", CancellationToken ct = default);

    /// <summary>
    /// SubscribeRoom 진입 전 세션·방·멤버십 검증
    /// </summary>
    /// <returns>성공 시 userId</returns>
    Task<Result<long>> ValidateSubscriptionAsync(string sessionId, long roomId, CancellationToken ct = default);

    /// <summary>
    /// 인게임에서 플레이어가 명시적으로 퇴장했을 때 그 플레이어의 방 association을 정리한다.
    /// SocketServer가 PlayerLeft 이벤트를 발행하면 RoomLifecycleConsumer가 호출한다.
    ///
    /// 동작: association 제거(재로그인 복원 차단) + 채팅 방 구독 해제.
    ///   - 남은 인원 0명  → 방 삭제(빈 방)
    ///   - 남은 인원 ≥1명 → 떠난 사람이 호스트면 다음 사람으로 이양 후 갱신/브로드캐스트
    /// 이미 그 방 소속이 아니면 멱등 성공(중복 소비 안전).
    /// </summary>
    Task<Result<DungeonRoom>> RemovePlayerFromRoomAsync(long roomId, long userId, CancellationToken ct = default);
}