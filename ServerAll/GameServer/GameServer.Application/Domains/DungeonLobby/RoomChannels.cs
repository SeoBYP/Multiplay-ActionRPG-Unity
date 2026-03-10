namespace GameServer.Application.Domains.DungeonLobby;

public static class RoomChannels
{
    public static string RoomChannel(long roomId) => $"game:room:{roomId}";
}