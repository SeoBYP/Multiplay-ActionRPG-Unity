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


    // Session
    public const string SessionNotFound = "SESSION_NOT_FOUND";


    // Request
    public const string InvalidRequest = "INVALID_REQUEST";
}