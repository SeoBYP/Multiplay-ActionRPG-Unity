using System.Security.Claims;
using GameServer.API.Services;
using GameServer.Grpc.Progression;
using GameServer.Tests.Infrastructure.Fakes.Repositories;
using Grpc.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.JsonWebTokens;
using AppProgressionService = GameServer.Application.Domains.Progression.ProgressionService;

namespace GameServer.Tests.API;

/// <summary>
/// ProgressionService gRPC 진입점 검증 — GetProgression(인증 + 레벨/Exp + 레벨 파생 스탯 합성).
/// 레벨업 로직 자체는 <see cref="Domain.Entities.UserProgressionTests"/>, 테이블 룩업은 LevelTableTests 가 담당.
/// 여기서는 gRPC 레이어(인증 + LevelTable 룩업 매핑)만 본다.
/// </summary>
public class ProgressionGrpcServiceTests
{
    private readonly FakeProgressionRepository _repository = new();
    private readonly ProgressionGrpcService _service;

    public ProgressionGrpcServiceTests()
    {
        _service = new ProgressionGrpcService(
            new AppProgressionService(_repository, new Infrastructure.Fakes.Services.FakeEquipmentService()),
            NullLogger<ProgressionGrpcService>.Instance);
    }

    [Fact]
    public async Task 적립_이력이_없으면_기본_Lv1_Exp0_과_Lv1_스탯을_반환한다()
    {
        var res = await _service.GetProgression(new GetProgressionRequest(), Authed(7L));

        Assert.True(res.Result.Success, res.Result.Message);
        Assert.Equal(1, res.Level);
        Assert.Equal(0, res.Exp);
        Assert.Equal(60, res.MaxLevel);
        Assert.Equal(100, res.ExpToNext);       // LevelTable.ExpToNext(1)
        Assert.Equal(100, res.Stats.MaxHealth); // Lv1 시드
        Assert.Equal(10, res.Stats.AttackPower);
    }

    [Fact]
    public async Task 레벨업_후에는_갱신된_레벨과_그_레벨의_스탯을_반환한다()
    {
        // Lv1 임계(100)를 넘기면 Lv2, remainder 20.
        await _repository.AddExpAsync(7L, 120);

        var res = await _service.GetProgression(new GetProgressionRequest(), Authed(7L));

        Assert.Equal(2, res.Level);
        Assert.Equal(20, res.Exp);
        Assert.Equal(283, res.ExpToNext);       // round(100 * 2^1.5)
        Assert.Equal(120, res.Stats.MaxHealth); // 100 + 1*20
        Assert.Equal(13, res.Stats.AttackPower); // 10 + 1*3
    }

    [Fact]
    public async Task 미인증_조회는_거부된다()
    {
        var res = await _service.GetProgression(new GetProgressionRequest(), Anonymous());

        Assert.False(res.Result.Success);
    }

    // ── 테스트 더블 ──

    private static ServerCallContext Context(ClaimsPrincipal user)
    {
        var ctx = new FakeServerCallContext();
        ctx.UserState["__HttpContext"] = new DefaultHttpContext { User = user };
        return ctx;
    }

    private static ServerCallContext Authed(long userId)
        => Context(new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()) }, "test")));

    private static ServerCallContext Anonymous()
        => Context(new ClaimsPrincipal(new ClaimsIdentity()));

    private sealed class FakeServerCallContext : ServerCallContext
    {
        private readonly Dictionary<object, object> _userState = new();

        protected override string MethodCore => "GetProgression";
        protected override string HostCore => string.Empty;
        protected override string PeerCore => string.Empty;
        protected override DateTime DeadlineCore => DateTime.UtcNow.AddMinutes(1);
        protected override Metadata RequestHeadersCore => new();
        protected override CancellationToken CancellationTokenCore => CancellationToken.None;
        protected override Metadata ResponseTrailersCore => new();
        protected override Status StatusCore { get; set; }
        protected override WriteOptions? WriteOptionsCore { get; set; }
        protected override AuthContext AuthContextCore => new(string.Empty, new Dictionary<string, List<AuthProperty>>());
        protected override IDictionary<object, object> UserStateCore => _userState;

        protected override ContextPropagationToken CreatePropagationTokenCore(ContextPropagationOptions? options)
            => throw new NotSupportedException();

        protected override Task WriteResponseHeadersAsyncCore(Metadata responseHeaders) => Task.CompletedTask;
    }
}
