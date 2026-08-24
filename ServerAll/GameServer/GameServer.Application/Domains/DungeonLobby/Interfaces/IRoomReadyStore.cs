namespace GameServer.Application.Domains.DungeonLobby.Interfaces;

/// <summary>
/// 방 대기실의 "준비 완료" 상태 저장소.
///
/// DB 가 아니라 Redis 전용이다 — 준비 상태는 방과 수명을 같이하는 휘발성 로비 상태이고,
/// 유실되면 전원 미준비로 되돌아갈 뿐 게임 데이터에 영구 손상이 없다.
/// 호스트는 이 저장소에 담기지 않는다(준비 개념 없음 = 항상 준비된 것으로 본다).
/// </summary>
public interface IRoomReadyStore
{
    /// <summary>한 플레이어의 준비 상태를 켜고 끈다.</summary>
    Task SetReadyAsync(long roomId, long userId, bool isReady, CancellationToken ct = default);

    /// <summary>이 방에서 준비 완료한 userId 집합.</summary>
    Task<IReadOnlySet<long>> GetReadyUserIdsAsync(long roomId, CancellationToken ct = default);

    /// <summary>여러 방의 준비 상태를 한 번에 조회한다(방 목록 화면의 N+1 왕복 회피).</summary>
    Task<IReadOnlyDictionary<long, IReadOnlySet<long>>> GetReadyUserIdsAsync(
        IReadOnlyCollection<long> roomIds, CancellationToken ct = default);

    /// <summary>방 전체 준비 상태를 지운다(방 삭제·게임 시작 시).</summary>
    Task ClearAsync(long roomId, CancellationToken ct = default);
}
