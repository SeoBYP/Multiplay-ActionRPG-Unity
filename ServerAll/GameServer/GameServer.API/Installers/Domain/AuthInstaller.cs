using GameServer.Application.Domains.Auth;
using GameServer.Application.Domains.Auth.Interfaces;
using GameServer.Application.Security.Interface;
using GameServer.Infrastructure.Interfaces;
using GameServer.Infrastructure.Security;

namespace GameServer.API.Installers.Domain;

public class AuthInstaller : IServiceInstaller
{
    public void Install(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IAuthService, AuthService>();
    }
}
