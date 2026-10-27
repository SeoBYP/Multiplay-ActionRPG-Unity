using GameServer.Application.Domains.User.Interfaces;
using GameServer.Domain.Entities.User;
using Microsoft.Extensions.Logging;
using Shared.Infrastructure.Spawn;

namespace GameServer.Application.Domains.User;

/// <summary>
/// Main 위치 지속화 구현. 서버가 검증하는 것은 맵 경계 하나다(`spawn-layouts` 가 맵 경계와 스폰 포인트를 안다).
/// 경계 밖 좌표를 clamp 하지 않고 저작 스폰으로 스냅하는 이유: clamp 는 경계선 위의 임의 점이라 벽 안일 수 있고,
/// 저작 스폰은 사람이 시작 가능하다고 찍은 안전한 점이다.
/// </summary>
public sealed class UserPositionService(
    IUserPositionRepository repository,
    ILogger<UserPositionService> logger) : IUserPositionService
{
    public async Task<SavePositionResult> SaveAsync(
        long userId, string mapId, float x, float y, float z, float rotY, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(mapId) || !SpawnLayoutTable.IsKnown(mapId))
        {
            logger.LogWarning("[Position] 알 수 없는 mapId={MapId} — 저장 거부 (user {UserId})", mapId, userId);
            return new SavePositionResult(Accepted: false, Snapped: false);
        }

        var layout = SpawnLayoutTable.Get(mapId);
        bool snapped = false;

        if (!layout.Bounds.Contains(x, z))
        {
            var fallback = NearestSpawn(layout, x, z);
            if (fallback is null)
            {
                // 스냅 대상이 없으면 저장하지 않는다(잘못된 좌표를 남기지 않는다)
                logger.LogWarning("[Position] 경계 밖인데 스냅할 스폰이 없다 mapId={MapId} (user {UserId})", mapId, userId);
                return new SavePositionResult(Accepted: false, Snapped: false);
            }

            logger.LogWarning(
                "[Position] 경계 밖 좌표 스냅 user={UserId} map={MapId} ({X},{Z}) → ({SnapX},{SnapZ})",
                userId, mapId, x, z, fallback.X, fallback.Z);

            x = fallback.X;
            y = fallback.Y;
            z = fallback.Z;
            snapped = true;
        }

        await repository.SaveVolatileAsync(UserPosition.Create(userId, mapId, x, y, z, rotY), ct);
        return new SavePositionResult(Accepted: true, Snapped: snapped);
    }

    public Task<UserPosition?> GetAsync(long userId, CancellationToken ct = default)
        => repository.GetAsync(userId, ct);

    public Task FlushAsync(long userId, CancellationToken ct = default)
        => repository.FlushToDatabaseAsync(userId, ct);

    /// <summary>XZ 평면 최근접 저작 스폰. 없으면 null.</summary>
    private static SpawnPoint? NearestSpawn(MapSpawnLayout layout, float x, float z)
    {
        SpawnPoint? best = null;
        float bestSq = float.MaxValue;

        foreach (var p in layout.Points)
        {
            float dx = p.X - x, dz = p.Z - z;
            float sq = dx * dx + dz * dz;
            if (sq < bestSq)
            {
                bestSq = sq;
                best = p;
            }
        }
        return best;
    }
}
