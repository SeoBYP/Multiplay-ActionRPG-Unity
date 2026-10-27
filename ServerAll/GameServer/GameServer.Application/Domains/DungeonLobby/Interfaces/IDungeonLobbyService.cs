using GameServer.Application.Common;
using GameServer.Domain.Entities;

namespace GameServer.Application.Domains.DungeonLobby.Interfaces;

public interface IDungeonLobbyService
{
    /// <param name="mapId">던전(spawn-layouts.json 키). 비우면 MapIds.Default, 알 수 없는 맵이면 실패.</param>
    Task<Result<DungeonRoom>> CreateDungeonRoomAsync(string sessionId, string roomName, int maxPlayers, string mapId = "", CancellationToken ct = default);

    /// <summary>활성 방 목록 한 페이지 + <b>전체</b> 활성 방 수.</summary>
    /// <param name="offset">건너뛸 방 수. 음수는 0 으로 clamp.</param>
    /// <param name="limit">가져올 방 수. 0 이하면 <see cref="DungeonLobbyPaging.DefaultPageSize"/>, 상한은 <see cref="DungeonLobbyPaging.MaxPageSize"/>.</param>
    /// <remarks>
    /// RoomId 내림차순(최신 먼저)으로 서버가 고정한다. Redis <c>room:active</c> 가 무순서 Set 이라
    /// 정렬 없이 offset 을 적용하면 페이지 간 중복·누락이 생긴다.
    /// </remarks>
    Task<Result<ActiveRoomPage>> GetActiveDungeonRoomsAsync(int offset, int limit, CancellationToken ct = default);

    Task<Result<DungeonRoom>> GetDungeonRoomAsync(long roomId, CancellationToken ct = default);

    Task<Result<DungeonRoom>> UpdateRoomSettingsAsync(string sessionId, long roomId,
        string? roomName = null, int? maxPlayers = null, CancellationToken ct = default);

    Task<Result<DungeonRoom>> JoinRoomAsync(string sessionId, long roomId, CancellationToken ct = default);

    Task<Result<DungeonRoom>> LeaveRoomAsync(string sessionId, long roomId, CancellationToken ct = default);

    /// <summary>대기실 준비 상태 토글 (호스트 제외). Redis 전용 휘발성 상태(<see cref="IRoomReadyStore"/>).</summary>
    Task<Result<DungeonRoom>> SetReadyAsync(string sessionId, long roomId, bool isReady, CancellationToken ct = default);

    /// <summary>게임 시작 (방장만). Outbox 기록까지만 하고 세션 생성은 GameSessionService 책임.</summary>
    /// <param name="mapId">비우면 MapIds.Default. 특정 맵을 강제할 때 지정.</param>
    Task<Result<DungeonRoom>> StartGameAsync(string sessionId, long roomId, string traceId, string mapId = "", CancellationToken ct = default);

    /// <summary>SubscribeRoom 진입 전 세션·방·멤버십 검증. 성공 시 userId.</summary>
    Task<Result<long>> ValidateSubscriptionAsync(string sessionId, long roomId, CancellationToken ct = default);

    /// <summary>
    /// 인게임 명시적 퇴장(PlayerLeft → RoomLifecycleConsumer)의 방 association 정리.
    /// association 제거(재로그인 복원 차단) + 채팅 방 구독 해제. 남은 인원 0명이면 방 삭제,
    /// 떠난 사람이 호스트면 다음 사람에게 이양. 이미 소속이 아니면 멱등 성공.
    /// </summary>
    Task<Result<DungeonRoom>> RemovePlayerFromRoomAsync(long roomId, long userId, CancellationToken ct = default);

    /// <summary>방 하나가 유령 방이면 정리한다. 정리했으면 true.</summary>
    /// <remarks>
    /// 방을 만들고 앱을 종료하면 소켓이 붙은 적이 없어 PlayerLeft 가 안 나오므로
    /// <see cref="RemovePlayerFromRoomAsync"/> 만으로는 영원히 남는다. 세션 활성 만료 시각을 근사 신호로 쓴다.
    /// 방 단위로 쪼갠 이유: 한 스코프로 여러 방을 처리하면 한 방의 저장 실패가 변경 추적을 오염시켜
    /// 뒤따르는 방까지 실패한다. 호출자(<c>DungeonRoomReaper</c>)가 방마다 새 스코프를 연다.
    /// </remarks>
    Task<Result<bool>> ReapRoomIfAbandonedAsync(long roomId, CancellationToken ct = default);
}