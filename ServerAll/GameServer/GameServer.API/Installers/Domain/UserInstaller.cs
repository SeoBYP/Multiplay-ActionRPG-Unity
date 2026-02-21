using GameServer.Application.Services.User;
using GameServer.Application.Services.User.Interfaces;
using GameServer.Infrastructure.Interfaces.User;
using GameServer.Infrastructure.Repositories.User;

namespace GameServer.API.Installers.Domain;

public class UserInstaller : IServiceInstaller
{
    public void Install(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IUserRepository, UserRepository>();
        services.AddSingleton<IUserSessionRepository, UserSessionRepository>();
        services.AddSingleton<IUserService, UserService>();
    }
}
