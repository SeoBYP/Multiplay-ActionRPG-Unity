namespace GameServer.Application.Domains.DungeonLobby;

public enum JoinRoomAtomicResult
{
    Success = 1,
    RoomNotFound = -1,
    InvalidStatus = -2,
    AlreadyInOtherRoom = -3,
    AlreadyInThisRoom = -4,
    RoomFull = -5,
    UnknownError = -999
}