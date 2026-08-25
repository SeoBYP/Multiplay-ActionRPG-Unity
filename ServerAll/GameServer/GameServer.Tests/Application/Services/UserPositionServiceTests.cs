using GameServer.Application.Domains.User;
using GameServer.Application.Domains.User.Interfaces;
using GameServer.Domain.Entities.User;
using Microsoft.Extensions.Logging.Abstractions;
using Shared.Infrastructure.Spawn;

namespace GameServer.Tests.Application.Services;

/// <summary>
/// Main 위치 지속화의 **신뢰 경계**(B7).
///
/// 좌표는 클라가 만든 값이다. 서버가 검증할 수 있는 재료는 `spawn-layouts` 의 맵 경계 하나뿐이라,
/// 경계 밖 좌표를 그대로 저장하지 않고 가장 가까운 저작 스폰으로 스냅한다.
/// (내비메시는 클라 자산이고 진입 게이트 시스템은 아직 없다 — 그래서 그 둘은 검증하지 않는다.)
/// </summary>
public class UserPositionServiceTests
{
    private const long UserId = 7L;

    private readonly FakeUserPositionRepository _repo = new();
    private readonly UserPositionService _service;

    public UserPositionServiceTests()
        => _service = new UserPositionService(_repo, NullLogger<UserPositionService>.Instance);

    /// <summary>main_field_01 의 실제 저작 경계(spawn-layouts bake).</summary>
    private static MapBounds MainBounds => SpawnLayoutTable.Get(MapIds.MainField01).Bounds;

    [Fact]
    public async Task 알수없는_mapId_는_거부한다()
    {
        var result = await _service.SaveAsync(UserId, "no_such_map", 0f, 0f, 0f, 0f);

        Assert.False(result.Accepted);
        Assert.Null(_repo.LastSaved);
    }

    [Fact]
    public async Task 경계_안_좌표는_그대로_저장한다()
    {
        var b = MainBounds;
        Assert.True(b.HasArea, "main_field_01 에 경계가 저작돼 있어야 이 테스트가 의미를 갖는다");

        float x = b.CenterX + 1f, z = b.CenterZ + 1f;
        var result = await _service.SaveAsync(UserId, MapIds.MainField01, x, 0.5f, z, 90f);

        Assert.True(result.Accepted);
        Assert.False(result.Snapped);
        Assert.Equal(x, _repo.LastSaved!.X, 0.001f);
        Assert.Equal(z, _repo.LastSaved!.Z, 0.001f);
        Assert.Equal(90f, _repo.LastSaved!.RotY, 0.001f);
    }

    [Fact]
    public async Task 경계_밖_좌표는_가장_가까운_저작_스폰으로_스냅한다()
    {
        var b = MainBounds;
        float farX = b.MaxX + 10_000f;   // 맵 밖 — 치터가 보냈든 버그든 그대로 저장하면 안 된다

        var result = await _service.SaveAsync(UserId, MapIds.MainField01, farX, 0f, b.CenterZ, 0f);

        Assert.True(result.Accepted);
        Assert.True(result.Snapped);

        var saved = _repo.LastSaved!;
        Assert.True(b.Contains(saved.X, saved.Z), "스냅 후에도 경계 밖이다");

        // 스냅 대상은 임의 clamp 가 아니라 **저작 스폰 포인트**여야 한다(지형 밖·벽 안 회피).
        var points = SpawnLayoutTable.Get(MapIds.MainField01).Points;
        Assert.Contains(points, p => Math.Abs(p.X - saved.X) < 0.001f && Math.Abs(p.Z - saved.Z) < 0.001f);
    }

    [Fact]
    public async Task 저장된_위치가_없으면_null_을_돌려준다()
    {
        Assert.Null(await _service.GetAsync(UserId));
    }

    [Fact]
    public async Task 저장한_위치를_그대로_조회한다()
    {
        var b = MainBounds;
        await _service.SaveAsync(UserId, MapIds.MainField01, b.CenterX, 1f, b.CenterZ, 45f);

        var got = await _service.GetAsync(UserId);

        Assert.NotNull(got);
        Assert.Equal(MapIds.MainField01, got!.MapId);
        Assert.Equal(45f, got.RotY, 0.001f);
    }

    [Fact]
    public async Task Flush_는_저장소의_확정_저장을_호출한다()
    {
        await _service.FlushAsync(UserId);

        Assert.Equal(UserId, _repo.FlushedUserId);
    }

    private sealed class FakeUserPositionRepository : IUserPositionRepository
    {
        public UserPosition? LastSaved;
        public long? FlushedUserId;

        public Task SaveVolatileAsync(UserPosition position, CancellationToken ct = default)
        {
            LastSaved = position;
            return Task.CompletedTask;
        }

        public Task<UserPosition?> GetAsync(long userId, CancellationToken ct = default)
            => Task.FromResult(LastSaved);

        public Task FlushToDatabaseAsync(long userId, CancellationToken ct = default)
        {
            FlushedUserId = userId;
            return Task.CompletedTask;
        }
    }
}
