using GameServer.Domain.Entities.Quest;
using GameServer.Infrastructure.Domains.Quest;
using GameServer.Infrastructure.Domains.User;
using Microsoft.Extensions.Logging.Abstractions;

namespace GameServer.Tests.Infrastructure.Integrations;

[Collection("RepositoryIntegrationTests")]
public class QuestRepositoryIntegrationTests
{
    private readonly RepositoryTestFixture _fixture;

    public QuestRepositoryIntegrationTests(RepositoryTestFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<long> CreateUserAsync()
    {
        using var context = _fixture.CreateDbContext();
        var userRepo = new UserRepository(_fixture.RedisConnection, context, NullLogger<UserRepository>.Instance);
        var user = await userRepo.CreateAsync();
        return user.UserId;
    }

    private QuestRepository CreateRepository()
        => new(_fixture.CreateDbContext(), NullLogger<QuestRepository>.Instance);

    [Fact]
    public async Task Upsert_없으면_insert하고_조회된다()
    {
        var userId = await CreateUserAsync();
        var repository = CreateRepository();

        await repository.UpsertAsync(UserQuest.Create(userId, "quest_slime_hunt"));

        var row = await repository.GetAsync(userId, "quest_slime_hunt");
        Assert.NotNull(row);
        Assert.Equal(QuestStatus.Accepted, row!.Status);
        Assert.Equal(0, row.Progress);
    }

    [Fact]
    public async Task Upsert_있으면_진행이_갱신된다()
    {
        var userId = await CreateUserAsync();
        var repository = CreateRepository();

        var quest = UserQuest.Create(userId, "quest_slime_hunt");
        await repository.UpsertAsync(quest);

        quest.AddProgress(2, 3);
        await repository.UpsertAsync(quest);

        var row = await repository.GetAsync(userId, "quest_slime_hunt");
        Assert.Equal(2, row!.Progress);

        using var ctx = _fixture.CreateDbContext();
        var dbRow = await ctx.UserQuests.FindAsync(userId, "quest_slime_hunt");
        Assert.Equal(2, dbRow!.Progress); // 행 중복 없이 갱신
    }

    [Fact]
    public async Task Claim_상태가_영속된다()
    {
        var userId = await CreateUserAsync();
        var repository = CreateRepository();

        var quest = UserQuest.Create(userId, "quest_slime_hunt");
        quest.AddProgress(3, 3);
        quest.Claim(3);
        await repository.UpsertAsync(quest);

        var row = await repository.GetAsync(userId, "quest_slime_hunt");
        Assert.Equal(QuestStatus.Claimed, row!.Status);
    }

    [Fact]
    public async Task 수주는_유저별로_격리된다()
    {
        var userA = await CreateUserAsync();
        var userB = await CreateUserAsync();
        var repository = CreateRepository();

        await repository.UpsertAsync(UserQuest.Create(userA, "quest_slime_hunt"));

        Assert.Empty(await repository.GetAllForUserAsync(userB));
    }
}
