namespace GameServer.Application.Common;

public class ErrorMessages
{
    // Service / Server
    public const string ServiceUnavailable = "SERVICE_UNAVAILABLE";
    public const string InternalServerError = "INTERNAL_SERVER_ERROR";

    // Authentication / Authorization
    public const string Unauthorized = "UNAUTHORIZED";
    public const string InvalidToken = "INVALID_TOKEN";
    public const string SessionExpired = "SESSION_EXPIRED";

    // User
    public const string UserAlreadyExists = "USER_ALREADY_EXISTS";
    public const string UserNotFound = "USER_NOT_FOUND";
    public const string InvalidCredentials = "INVALID_CREDENTIALS";
    public const string NickNameAlreadyTaken = "NICKNAME_ALREADY_TAKEN";
    public const string EmailAlreadyTaken = "EMAIL_ALREADY_TAKEN";
    public const string Profanity = "PROFANITY";
    
    // DungeonRoom
    public const string RoomNotFound = "ROOM_NOT_FOUND";
    public const string RoomFull = "ROOM_FULL";
    public const string AlreadyInRoom = "ALREADY_IN_ROOM";
    public const string NotInRoom = "NOT_IN_ROOM";
    public const string NotRoomHost = "NOT_ROOM_HOST";
    public const string RoomNotWaiting = "ROOM_NOT_WAITING";
    public const string RoomAlreadyPlaying = "ROOM_ALREADY_PLAYING";
    public const string RoomClosed = "ROOM_CLOSED";
    public const string JoinRoomFailed = "JOIN_ROOM_FAILED";
    public const string LeaveRoomFailed = "LEAVE_ROOM_FAILED";
    public const string StartGameFailed = "START_GAME_FAILED";
    
    // Session
    public const string SessionNotFound = "SESSION_NOT_FOUND";
    
    // ChatMessage
    public const string MessageNotFound = "MESSAGE_NOT_FOUND";

    // Request
    public const string InvalidRequest = "INVALID_REQUEST";

}
