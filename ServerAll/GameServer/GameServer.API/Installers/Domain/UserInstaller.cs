using GameServer.Application.Domains.User;
using GameServer.Application.Domains.User.Interfaces;
using GameServer.Infrastructure.Domains.User;

namespace GameServer.API.Installers.Domain;

public class UserInstaller : IServiceInstaller
{
    public void Install(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IUserCredentialRepository, UserCredentialRepository>();
        services.AddScoped<IUserSessionRepository, UserSessionRepository>();
        services.AddScoped<IUserProfileRepository, UserProfileRepository>();  // 추가
        services.AddScoped<IUserProfileService, UserProfileService>();
    }
}
