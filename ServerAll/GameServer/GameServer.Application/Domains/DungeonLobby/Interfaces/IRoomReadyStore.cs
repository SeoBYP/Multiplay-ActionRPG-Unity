namespace GameServer.Application.Domains.DungeonLobby.Interfaces;

/// <summary>
/// 대기실 "준비 완료" 저장소. Redis 전용 — 유실돼도 전원 미준비로 돌아갈 뿐이다.
/// 호스트는 담기지 않는다(항상 준비된 것으로 본다).
/// </summary>
public interface IRoomReadyStore
{
    Task SetReadyAsync(long roomId, long userId, bool isReady, CancellationToken ct = default);

    Task<IReadOnlySet<long>> GetReadyUserIdsAsync(long roomId, CancellationToken ct = default);

    /// <summary>여러 방의 준비 상태를 한 번에 조회한다(방 목록 화면의 N+1 왕복 회피).</summary>
    Task<IReadOnlyDictionary<long, IReadOnlySet<long>>> GetReadyUserIdsAsync(
        IReadOnlyCollection<long> roomIds, CancellationToken ct = default);

    /// <summary>방 전체 준비 상태를 지운다(방 삭제·게임 시작 시).</summary>
    Task ClearAsync(long roomId, CancellationToken ct = default);
}
