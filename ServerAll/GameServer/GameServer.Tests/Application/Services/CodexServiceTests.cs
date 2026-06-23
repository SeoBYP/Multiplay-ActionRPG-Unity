using GameServer.Application.Domains.Codex;
using GameServer.Tests.Infrastructure.Fakes.Repositories;

namespace GameServer.Tests.Application.Services;

public class CodexServiceTests
{
    private readonly FakeCodexRepository _repository = new();
    private readonly CodexService _service;

    public CodexServiceTests()
    {
        _service = new CodexService(_repository);
    }

    [Fact]
    public async Task 발견_이력이_없으면_빈_목록을_반환한다()
    {
        var discovered = await _service.GetDiscoveredAsync(1L);

        Assert.Empty(discovered);
    }

    [Fact]
    public async Task 첫_발견은_true_이고_조회에_나타난다()
    {
        var first = await _service.MarkDiscoveredAsync(1L, "potion_hp_small");

        Assert.True(first);
        Assert.Contains("potion_hp_small", await _service.GetDiscoveredAsync(1L));
    }

    [Fact]
    public async Task 이미_발견한_아이템_재발견은_false_멱등()
    {
        await _service.MarkDiscoveredAsync(1L, "potion_hp_small");

        var second = await _service.MarkDiscoveredAsync(1L, "potion_hp_small");

        Assert.False(second);
        Assert.Single(await _service.GetDiscoveredAsync(1L));
    }

    [Fact]
    public async Task 카탈로그에_없는_itemId는_발견_기록되지_않는다()
    {
        var marked = await _service.MarkDiscoveredAsync(1L, "unknown_item");

        Assert.False(marked);
        Assert.Empty(await _service.GetDiscoveredAsync(1L));
    }

    [Fact]
    public async Task 발견은_유저별로_격리된다()
    {
        await _service.MarkDiscoveredAsync(1L, "potion_hp_small");

        Assert.Empty(await _service.GetDiscoveredAsync(2L));
    }
}
