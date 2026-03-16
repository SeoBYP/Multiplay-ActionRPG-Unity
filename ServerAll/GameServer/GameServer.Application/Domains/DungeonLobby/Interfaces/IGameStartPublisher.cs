using Shared.Infrastructure.Messages;

namespace GameServer.Application.Domains.DungeonLobby.Interfaces;

public interface IGameStartPublisher
{
    Task PublishAsync(GameStartMessage message, CancellationToken ct = default);
}