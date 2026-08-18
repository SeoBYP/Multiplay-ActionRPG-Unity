using System.Security.Claims;
using GameServer.API.Services;
using GameServer.Grpc.Codex;
using GameServer.Tests.Infrastructure.Fakes.Repositories;
using Grpc.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.JsonWebTokens;
using AppCodexService = GameServer.Application.Domains.Codex.CodexService;

namespace GameServer.Tests.API;

/// <summary>
/// CodexService gRPC 진입점 검증 — GetCodex(인증 + 발견 itemId 집합 반환).
/// 발견 기록/멱등 로직은 <see cref="Application.Services.CodexServiceTests"/> 가 담당.
/// 여기서는 gRPC 레이어(인증 + 매핑)만 본다.
/// </summary>
public class CodexGrpcServiceTests
{
    private readonly FakeCodexRepository _repository = new();
    private readonly CodexGrpcService _service;

    public CodexGrpcServiceTests()
    {
        _service = new CodexGrpcService(new AppCodexService(_repository), NullLogger<CodexGrpcService>.Instance);
    }

    [Fact]
    public async Task 발견한_아이템_목록을_반환한다()
    {
        await _repository.AddDiscoveredAsync(7L, 1001);
        await _repository.AddDiscoveredAsync(7L, 2101);

        var res = await _service.GetCodex(new GetCodexRequest(), Authed(7L));

        Assert.True(res.Result.Success, res.Result.Message);
        Assert.Equal(2, res.DiscoveredItemIds.Count);
        Assert.Contains(1001, res.DiscoveredItemIds);
        Assert.Contains(2101, res.DiscoveredItemIds);
    }

    [Fact]
    public async Task 발견_이력이_없으면_빈_목록을_반환한다()
    {
        var res = await _service.GetCodex(new GetCodexRequest(), Authed(7L));

        Assert.True(res.Result.Success, res.Result.Message);
        Assert.Empty(res.DiscoveredItemIds);
    }

    [Fact]
    public async Task 미인증_조회는_거부된다()
    {
        var res = await _service.GetCodex(new GetCodexRequest(), Anonymous());

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

        protected override string MethodCore => "GetCodex";
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
