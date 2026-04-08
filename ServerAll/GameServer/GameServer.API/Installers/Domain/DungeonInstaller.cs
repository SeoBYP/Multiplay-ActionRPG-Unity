using GameServer.Application.Domains.DungeonLobby;
using GameServer.Application.Domains.DungeonLobby.Interfaces;
using GameServer.Application.Domains.GameSession;
using GameServer.Application.Domains.GameSession.Interfaces;
using GameServer.Application.Domains.Outbox;
using GameServer.Infrastructure.Common;
using GameServer.Infrastructure.Common.MessageQueue;
using GameServer.Infrastructure.Domains.DungeonRoom;
using GameServer.Infrastructure.Domains.GameSession;
using GameServer.Infrastructure.Domains.Outbox;
using Shared.Infrastructure.MessageQueue;
using Shared.Infrastructure.Messages;

namespace GameServer.API.Installers.Domain;

public class DungeonInstaller : IServiceInstaller
{
    public void Install(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IDungeonLobbyService, DungeonLobbyService>();
        services.AddSingleton<IGameSessionService, GameSessionService>();

        services.AddSingleton<IDungeonRoomRepository, DungeonRoomRepository>();
        services.AddSingleton<IDungeonRoomPlayerRepository, DungeonRoomPlayerRepository>();
        services.AddSingleton<IDungeonLobbySubscriptionService, DungeonLobbySubscriptionService>();
        services.AddSingleton<IDungeonRoomEventStream, DungeonRoomEventStream>();
        services.AddSingleton<IGameSessionRepository, GameSessionRepository>();
        services.AddSingleton<IGameSessionPlayerRepository, GameSessionPlayerRepository>();

        services.AddSingleton<DungeonRoomBroadcastChannel>();
        services.AddSingleton<IMessageQueue<GameStartRequestedMessage>, GameStartRequestedMessageQueue>();
        services.AddSingleton<IMessageQueue<GameSessionReadyMessage>, GameSessionReadyMessageQueue>();

        services.AddSingleton<IOutboxRepository, OutboxRepository>();
        services.AddHostedService<OutboxPublisherService>();
        services.AddHostedService<GameSessionReadyConsumer>();
    }
}
