using GameServer.Application.Common;
using GrpcResult = GameServer.Grpc.Common.Result;

namespace GameServer.API.Extensions;

/// <summary>
/// Application Layer의 Result를 gRPC Result로 변환하는 확장 메서드
/// </summary>
public static class ResultExtensions
{
    /// <summary>
    /// Result<T>를 gRPC Result로 변환
    /// </summary>
    public static GrpcResult ToGrpcResult<T>(this Result<T> result) where T : class
    {
        return new GrpcResult
        {
            Success = result.IsSuccess,
            Message = result.Message ?? string.Empty,
            ErrorCode = (int)result.InternalErrorCode,
        };
    }
    
    /// <summary>
    /// Result를 gRPC Result로 변환
    /// </summary>
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