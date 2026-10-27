using GameServer.API.Installers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace GameServer.Tests.Infrastructure.Integrations;

/// <summary>
/// G1 — GameServer 기동이 Redis 를 비워서는 안 된다.
///
/// <c>ServiceInstaller.Install</c> 이 <c>#if DEBUG</c> 밖에서 <c>FlushAllDatabases()</c> 를 부른다.
/// Redis 에는 GameServer 혼자 쓰는 캐시만 있는 게 아니다:
///   · 아직 소비 안 된 보상 스트림 + 컨슈머 그룹 PEL  → 재시작하면 보상 영구 미지급
///   · Main 위치(UserPositionRepository — Redis 가 1차 저장소)
///   · SocketServer 가 읽는 gamesession:player:*   → 진행 중인 던전 재접속 불가
/// </summary>
[Collection("RepositoryIntegrationTests")]
public class RedisStartupPreservationTests(RepositoryTestFixture fixture)
{
    [Fact]
    public async Task GameServer_서비스_설치는_기존_Redis_데이터를_지우지_않는다()
    {
        var db = fixture.RedisConnection.GetDatabase();
        var pendingReward = $"test:stream:pending:{Guid.NewGuid():N}";
        var socketSession = $"test:gamesession:player:{Guid.NewGuid():N}";
        await db.StreamAddAsync(pendingReward, "payload", "dungeon-clear");
        await db.HashSetAsync(socketSession, "roomId", 1);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Redis"] = fixture.RedisConnectionString,
            })
            .Build();

        var services = new ServiceCollection();
        new ServiceInstaller().Install(services, configuration);
        await using (var provider = services.BuildServiceProvider())
            provider.GetRequiredService<IConnectionMultiplexer>();

        Assert.True(await db.KeyExistsAsync(pendingReward), "기동이 미소비 보상 스트림을 지웠다.");
        Assert.True(await db.KeyExistsAsync(socketSession), "기동이 SocketServer 의 세션 배정을 지웠다.");
    }
}
