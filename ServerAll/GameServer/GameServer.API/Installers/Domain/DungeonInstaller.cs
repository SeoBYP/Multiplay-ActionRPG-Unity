using GameServer.Application.Services.DungeonLobby;
using GameServer.Application.Services.DungeonLobby.Interfaces;
using GameServer.Infrastructure.Interfaces.DungeonRoom;
using GameServer.Infrastructure.Repositories.DungeonRoom;

namespace GameServer.API.Installers.Domain;

public class DungeonInstaller : IServiceInstaller
{
    public void Install(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IDungeonLobbyService, DungeonLobbyService>();
        services.AddScoped<IDungeonRoomRepository, DungeonRoomRepository>();
    }
}
