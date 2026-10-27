using GameServer.Domain.Entities.User;

namespace GameServer.Application.Domains.User.Interfaces;

/// <param name="Accepted">저장했으면 true. 알 수 없는 mapId 등으로 거부했으면 false.</param>
/// <param name="Snapped">경계 밖이라 저작 스폰으로 스냅했으면 true(관측용 — 클라 동작은 바뀌지 않는다).</param>
public readonly record struct SavePositionResult(bool Accepted, bool Snapped);

/// <summary>
/// Main 위치 지속화. 클라가 주기 보고 → 재접속 시 그 자리에서 시작.
/// 서버가 검증하는 것은 맵 경계 하나뿐이다(내비메시는 클라 자산). 이동 궤적·근접 검증은 하지 않는다 — 좌표 자체가 클라가 만든 값이라 순환이다.
/// </summary>
public interface IUserPositionService
{
    /// <summary>위치 보고. 알 수 없는 mapId 는 거부, 맵 경계 밖이면 가장 가까운 저작 스폰으로 스냅해 저장한다. Redis 에만 쓴다(확정은 <see cref="FlushAsync"/>).</summary>
    Task<SavePositionResult> SaveAsync(long userId, string mapId, float x, float y, float z, float rotY, CancellationToken ct = default);

    /// <summary>마지막 위치. 없으면 null → 호출자(클라)는 저작 스폰으로 폴백한다.</summary>
    Task<UserPosition?> GetAsync(long userId, CancellationToken ct = default);

    /// <summary>휘발 저장소(Redis)의 최신값을 DB 로 확정한다. 이탈 시점(로그아웃·던전 입장)에 호출. 위치가 없으면 no-op.</summary>
    Task FlushAsync(long userId, CancellationToken ct = default);
}
