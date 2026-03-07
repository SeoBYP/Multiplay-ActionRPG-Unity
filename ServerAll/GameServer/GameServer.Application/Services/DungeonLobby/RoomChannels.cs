namespace GameServer.Application.Services.DungeonLobby;

public static class RoomChannels
{
    public static string RoomChannel(long roomId) => $"game:room:{roomId}";
}