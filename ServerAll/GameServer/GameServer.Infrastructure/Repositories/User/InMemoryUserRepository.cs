using GameServer.Domain.Entities;
using GameServer.Domain.Interfaces.User;

namespace GameServer.Infrastructure.Repositories.User;

public class InMemoryUserRepository : IUserRepository
{
    private readonly Dictionary<long, Domain.Entities.User.User> _users = new();
    
    private long _nextId = 1;
    
    public Task<Domain.Entities.User.User?> GetByUsernameAsync(string userName)
    {
        if(string.IsNullOrWhiteSpace(userName)) 
            return Task.FromResult<Domain.Entities.User.User?>(null);
        
        return Task.FromResult(_users.Values.FirstOrDefault(u => u.UserName == userName));
    }

    public Task AddAsync(Domain.Entities.User.User user)
    {
        if(user is null) throw new ArgumentNullException(nameof(user));
        
        user.SetUserId(_nextId++);
        _users[user.UserId] = user;
        return Task.CompletedTask;
    }

    public Task<Domain.Entities.User.User?> GetByIdAsync(long userId)
    {
        return Task.FromResult(_users.GetValueOrDefault(userId));
    }
}