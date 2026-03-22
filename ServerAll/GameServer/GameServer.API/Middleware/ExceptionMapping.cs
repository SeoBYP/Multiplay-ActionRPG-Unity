using System.Net;
using GameServer.Application.Common;
using Grpc.Core;
using StackExchange.Redis;

namespace GameServer.API.Middleware;

public sealed record ExceptionMapping(
    HttpStatusCode HttpStatusCode,
    StatusCode GrpcStatusCode,
    string Message);

public static class ExceptionMapper
{
    public static ExceptionMapping Map(Exception exception)
    {
        return exception switch
        {
            RedisConnectionException =>
                new ExceptionMapping(HttpStatusCode.ServiceUnavailable, StatusCode.Unavailable, ErrorMessages.ServiceUnavailable),
            RedisTimeoutException =>
                new ExceptionMapping(HttpStatusCode.ServiceUnavailable, StatusCode.Unavailable, ErrorMessages.ServiceUnavailable),
            UnauthorizedAccessException =>
                new ExceptionMapping(HttpStatusCode.Unauthorized, StatusCode.Unauthenticated, ErrorMessages.Unauthorized),
            ArgumentException =>
                new ExceptionMapping(HttpStatusCode.BadRequest, StatusCode.InvalidArgument, ErrorMessages.InvalidRequest),
            InvalidOperationException =>
                new ExceptionMapping(HttpStatusCode.BadRequest, StatusCode.InvalidArgument, ErrorMessages.InvalidRequest),
            _ =>
                new ExceptionMapping(HttpStatusCode.InternalServerError, StatusCode.Internal, ErrorMessages.InternalServerError)
        };
    }
}
