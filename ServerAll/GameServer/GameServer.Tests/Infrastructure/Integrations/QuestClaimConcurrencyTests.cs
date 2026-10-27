using GameServer.Application.Domains.Inventory.Interfaces;
using GameServer.Application.Domains.Progression.Interfaces;
using GameServer.Application.Domains.Quest;
using GameServer.Application.Domains.Wallet.Interfaces;
using GameServer.Domain.Entities.Quest;
using GameServer.Infrastructure.Domains.Quest;
using GameServer.Infrastructure.Domains.User;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace GameServer.Tests.Infrastructure.Integrations;

/// <summary>
/// G2 — 퀘스트 보상 중복 수령.
///
/// <c>ClaimRewardAsync</c> 는 "Status != Claimed" 를 <b>메모리에서</b> 확인한 뒤 Upsert 하고 지급한다.
/// 두 요청이 동시에 Accepted 를 읽으면 둘 다 Claimed 로 덮어쓰고(값이 같아 에러도 없다) 둘 다 지급한다.
/// 단위 테스트(FakeQuestRepository)는 await 사이에 끼어들 틈이 없어 이 경합을 못 본다 → 실제 DB 로 친다.
///
/// 지급 쪽은 Mock 으로 둔다 — 실제 지갑 저장소는 그 자체가 lost update(G3)라 이중 지급을 가려 버린다.
/// </summary>
[Collection("RepositoryIntegrationTests")]
public class QuestClaimConcurrencyTests(RepositoryTestFixture fixture)
{
    private const string QuestId = "quest_slime_hunt"; // slime×3 → exp50 + gold100
    private const int Concurrency = 10;

    [Fact]
    public async Task 완료한_퀘스트_보상을_동시에_여러_번_요청해도_한_번만_지급된다()
    {
        long userId;
        using (var ctx = fixture.CreateDbContext())
            userId = (await new UserRepository(fixture.RedisConnection, ctx, NullLogger<UserRepository>.Instance).CreateAsync()).UserId;

        var quest = UserQuest.Create(userId, QuestId);
        quest.AddProgress(3, 3);
        await new QuestRepository(fixture.CreateDbContext(), NullLogger<QuestRepository>.Instance).UpsertAsync(quest);

        var wallet = new Mock<IWalletService>();
        var progression = new Mock<IProgressionService>();
        var inventory = new Mock<IInventoryService>();

        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var claims = Enumerable.Range(0, Concurrency).Select(_ => Task.Run(async () =>
        {
            // 요청마다 스코프(DbContext) 분리 — 실제 gRPC 요청과 같다.
            var service = new QuestService(
                new QuestRepository(fixture.CreateDbContext(), NullLogger<QuestRepository>.Instance),
                progression.Object, wallet.Object, inventory.Object,
                NullLogger<QuestService>.Instance);
            await gate.Task;
            try { return (await service.ClaimRewardAsync(userId, QuestId)).Success; }
            catch { return false; }
        })).ToList();

        gate.SetResult();
        var results = await Task.WhenAll(claims);

        Assert.Equal(1, results.Count(ok => ok));
        wallet.Verify(w => w.AddAsync(userId, It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Once());
        progression.Verify(p => p.AddExpAsync(userId, It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Once());
    }
}
