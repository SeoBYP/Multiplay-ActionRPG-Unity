namespace GameServer.Application.Common;

public enum ErrorCodes : ushort
{
    Success = 200,
    
    Unauthorized = 400,
    
    UserAlreadyExists = 1000,
    InvalidCredentials = 1001,
    InvalidToken = 1002,
    UserNotFound = 1003,
    SessionNotFound = 1004,
    InvalidRequest = 1005,
    SessionExpired = 1006,
    
    RoomNotFound = 2000,
    RoomFull = 2001,
    AlreadyInRoom = 2002,
    NotInRoom = 2003,
    NotRoomHost = 2004,
    RoomNotWaiting = 2005,
    RoomAlreadyPlaying = 2006,
    RoomClosed = 2007,
    JoinRoomFailed = 2010,   
    LeaveRoomFailed = 2011,  
    StartGameFailed = 2012,
    UpdateRoomFailed = 2013,
    
    MessageNotFound = 3000,
    
    InternalServerError = 5000,
}
