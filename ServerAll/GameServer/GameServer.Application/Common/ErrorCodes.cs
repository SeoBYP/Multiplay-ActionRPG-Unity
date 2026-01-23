namespace GameServer.Application.Common;

public enum ErrorCodes : ushort
{
    UserAlreadyExists = 1000,
    InvalidCredentials = 1001,
    InvalidToken = 1002,
    UserNotFound = 1003,
    SessionNotFound = 1004,
    InvalidRequest = 1005,
    SessionExpired = 1006,
}