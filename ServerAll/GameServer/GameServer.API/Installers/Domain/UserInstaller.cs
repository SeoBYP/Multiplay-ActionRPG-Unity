using GameServer.Application.Domains.Progression;
using GameServer.Application.Domains.Progression.Interfaces;
using GameServer.Application.Domains.User;
using GameServer.Application.Domains.User.Interfaces;
using GameServer.Infrastructure.Domains.Progression;
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

        // Progression (레벨·경험치) — user 와 1:1 이라 여기서 등록.
        services.AddScoped<IProgressionRepository, ProgressionRepository>();
        services.AddScoped<IProgressionService, ProgressionService>();
    }
}
