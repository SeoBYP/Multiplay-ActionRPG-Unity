using GameServer.Domain.Entities;
using GameServer.Domain.Interfaces;

namespace GameServer.Infrastructure.Repositories;

public class UserRepository : IUserRepository
{
    // TODO : 동시 접속자
    public Task<User?> GetByUsernameAsync(string userName)
    {
        throw new NotImplementedException();
    }

    public Task AddAsync(User user)
    {
        throw new NotImplementedException();
    }

    public Task<User?> GetByIdAsync(long userId)
    {
        throw new NotImplementedException();
    }
}