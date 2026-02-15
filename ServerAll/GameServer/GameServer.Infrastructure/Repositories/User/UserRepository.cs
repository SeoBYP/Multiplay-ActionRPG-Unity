using GameServer.Domain.Entities;
using GameServer.Infrastructure.Interfaces.User;

namespace GameServer.Infrastructure.Repositories.User;

public class UserRepository : IUserRepository
{
    // TODO : 동시 접속자
    public Task<Domain.Entities.User.User?> GetByUsernameAsync(string userName)
    {
        throw new NotImplementedException();
    }

    public Task AddAsync(Domain.Entities.User.User user)
    {
        throw new NotImplementedException();
    }

    public Task<Domain.Entities.User.User?> GetByIdAsync(long userId)
    {
        throw new NotImplementedException();
    }
}