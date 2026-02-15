using GameServer.Domain.Entities.Chat;
using MemoryPack;

namespace GameServer.Application.Common;

[MemoryPackable]
public partial class Result<T> where T : class
{
    public bool IsSuccess { get; private set; }
    public T? Value { get; private set; }

     public ErrorCodes InternalErrorCode { get; private set; }

    public string? Message { get; private set; }

    public static Result<T?> Success(T value)
    {
        return new Result<T> { IsSuccess = true, Message = "Success", InternalErrorCode = ErrorCodes.Success, Value = value };
    }

    public static Result<T> Failure(ErrorCodes errorCode, string message)
    {
        return new Result<T> { IsSuccess = false, InternalErrorCode = errorCode, Message = message };
    }
}

[MemoryPackable]
public partial class Result
{
    public bool IsSuccess { get; private set; }

    public ErrorCodes InternalErrorCode { get; private set; }

    public string? Message { get; private set; }

    public static Result Success() => new Result { IsSuccess = true, Message = "Success", InternalErrorCode = ErrorCodes.Success };

    public static Result Failure(ErrorCodes errorCode, string message) => new Result
        { IsSuccess = false, InternalErrorCode = errorCode, Message = message };
}
