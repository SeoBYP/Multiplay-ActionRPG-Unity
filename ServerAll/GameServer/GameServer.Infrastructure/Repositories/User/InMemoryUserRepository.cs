using GameServer.Domain.Entities;

public class InMemoryUserRepository : IUserRepository
{
    private readonly Dictionary<long, User> _users = new();
    
    private long _nextId = 1;
    
    public Task<User?> GetByUsernameAsync(string userName)
    {
        if(string.IsNullOrWhiteSpace(userName)) 
            return Task.FromResult<User?>(null);
        
        return Task.FromResult(_users.Values.FirstOrDefault(u => u.UserName == userName));
    }

    public Task AddAsync(User user)
    {
        if(user is null) throw new ArgumentNullException(nameof(user));
        
        user.SetUserId(_nextId++);
        _users[user.UserId] = user;
        return Task.CompletedTask;
    }

    public Task<User?> GetByIdAsync(long userId)
    {
        return Task.FromResult(_users.GetValueOrDefault(userId));
    }
}