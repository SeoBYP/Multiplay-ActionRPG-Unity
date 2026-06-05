using GameServer.Application.Domains.DungeonLobby;
using GameServer.Application.Domains.DungeonLobby.Interfaces;
using GameServer.Application.Domains.GameSession;
using GameServer.Application.Domains.GameSession.Interfaces;
using GameServer.Application.Domains.Outbox;
using GameServer.Infrastructure.Common;
using GameServer.Infrastructure.Common.Consumer;
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
        services.AddScoped<IGameSessionService, GameSessionService>();

        services.AddScoped<IDungeonRoomRepository, DungeonRoomRepository>();
        services.AddScoped<IDungeonRoomPlayerRepository, DungeonRoomPlayerRepository>();
        services.AddSingleton<IDungeonLobbySubscriptionService, DungeonLobbySubscriptionService>();
        services.AddSingleton<IDungeonRoomEventStream, DungeonRoomEventStream>();
        services.AddScoped<IGameSessionRepository, GameSessionRepository>();
        services.AddScoped<IGameSessionPlayerRepository, GameSessionPlayerRepository>();

        services.AddSingleton<DungeonRoomBroadcastChannel>();
        services.AddSingleton<IMessageQueue<GameStartRequestedMessage>, GameStartRequestedMessageQueue>();
        services.AddSingleton<IMessageQueue<GameSessionReadyMessage>, GameSessionReadyMessageQueue>();
        services.AddSingleton<IMessageQueue<PlayerLeftRoomMessage>, PlayerLeftRoomMessageQueue>();
        services.AddSingleton<IMessageQueue<DungeonClearMessage>, DungeonClearMessageQueue>();

        services.AddScoped<IOutboxRepository, OutboxRepository>();
        services.AddHostedService<OutboxPublisherService>();
        services.AddHostedService<GameSessionReadyConsumer>();
        services.AddHostedService<RoomLifecycleConsumer>();
        services.AddHostedService<DungeonResultConsumer>();
    }
}
