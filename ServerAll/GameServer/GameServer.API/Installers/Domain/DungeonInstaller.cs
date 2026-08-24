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
        services.AddSingleton<IRoomReadyStore, RedisRoomReadyStore>();
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

        // 유령 방 정리 — 소켓이 붙은 적 없는 방은 PlayerLeft 이벤트가 안 나와 영원히 남는다.
        services.Configure<DungeonRoomReaperOptions>(configuration.GetSection("DungeonRoomReaper"));
        services.AddHostedService<DungeonRoomReaper>();
    }
}
