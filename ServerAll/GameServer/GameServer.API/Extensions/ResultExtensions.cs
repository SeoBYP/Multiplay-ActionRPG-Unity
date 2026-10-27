using GameServer.Application.Common;
using GrpcResult = GameServer.Grpc.Common.Result;

namespace GameServer.API.Extensions;

public static class ResultExtensions
{
    public static GrpcResult ToGrpcResult<T>(this Result<T> result) where T : class
    {
        return new GrpcResult
        {
            Success = result.IsSuccess,
            Message = result.Message ?? string.Empty,
            ErrorCode = (int)result.InternalErrorCode,
        };
    }
    
    public static GrpcResult ToGrpcResult(this Result result)
    {
        return new GrpcResult
        {
            Success = result.IsSuccess,
            Message = result.Message ?? string.Empty,
            ErrorCode = (int)result.InternalErrorCode,
        };
    }

    public static GrpcResult CreateFailureGrpcResult()
    {
        return new GrpcResult
        {
            Success = false,
            ErrorCode = (int)ErrorCodes.InternalServerError,
            Message = ErrorMessages.InternalServerError,
        };
    }

    public static GrpcResult CreateUnauthorizedGrpcResult()
    {
        return new GrpcResult
        {
            Success = false,
            ErrorCode = (int)ErrorCodes.Unauthorized,
            Message = ErrorMessages.Unauthorized,
        };
    }
}