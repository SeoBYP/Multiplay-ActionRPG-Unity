using GameServer.Application.Common.Interfaces;
using GameServer.Infrastructure.Common;

namespace GameServer.API.Installers.Domain;

public class CommonInstaller : IServiceInstaller
{
    public void Install(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IUserLock, RedisUserLock>();
        services.AddSingleton<IProfanityFilter, ProfanityFilter>();
    }
}