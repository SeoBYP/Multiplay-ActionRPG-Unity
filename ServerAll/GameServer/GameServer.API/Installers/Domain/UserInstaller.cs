using GameServer.Application.Domains.User;
using GameServer.Application.Domains.User.Interfaces;
using GameServer.Infrastructure.Domains.User;

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
