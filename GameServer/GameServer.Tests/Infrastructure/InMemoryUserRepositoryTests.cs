using GameServer.Domain.Entities;
using GameServer.Infrastructure.Repositories;

namespace GameServer.Tests.Infrastructure;

public class InMemoryUserRepositoryTests
{
    [Fact]
    public async Task AddAsync_는_User를_저장한다()
    {
        // given
        var repository = new InMemoryUserRepository();
        var user = User.Create("testuser", "hash", "test@test.com");
        
        // when
        await repository.AddAsync(user);
        
        // then
        var found = await repository.GetByUsernameAsync("testuser");
        Assert.NotNull(found);
    }
}