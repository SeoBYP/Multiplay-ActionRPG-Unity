using Shared.Infrastructure.Messages;

namespace GameServer.Application.Domains.DungeonLobby.Interfaces;

public interface IGameStartPublisher
{
    Task PublishGameStartAsync(GameStartRequestedMessage message, CancellationToken ct = default);
}
