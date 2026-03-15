using GameServer.Application.Domains.DungeonLobby;
using GameServer.Application.Domains.DungeonLobby.Interfaces;
using GameServer.Infrastructure.Domains.DungeonRoom;

namespace GameServer.API.Installers.Domain;

public class DungeonInstaller : IServiceInstaller
{
    public void Install(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IDungeonLobbyService, DungeonLobbyService>();
        services.AddSingleton<IDungeonRoomRepository, DungeonRoomRepository>();
        services.AddSingleton<IDungeonLobbySubscriptionService, DungeonLobbySubscriptionService>();
        services.AddSingleton<DungeonRoomBroadcastChannel>();
        services.AddSingleton<IDungeonRoomEventStream, DungeonRoomEventStream>();

    }
}
