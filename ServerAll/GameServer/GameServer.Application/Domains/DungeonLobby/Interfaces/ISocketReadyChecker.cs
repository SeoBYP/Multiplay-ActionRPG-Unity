namespace GameServer.Application.Domains.DungeonLobby.Interfaces;

public interface ISocketReadyChecker
{
    Task<string?> WaitAsync(long roomId, CancellationToken ct = default);
}