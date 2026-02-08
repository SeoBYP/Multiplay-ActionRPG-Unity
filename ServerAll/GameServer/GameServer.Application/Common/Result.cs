using MemoryPack;

namespace GameServer.Application.Common;

[MemoryPackable]
public partial class Result<T> where T : class
{
    public bool IsSuccess { get; set; }
    public T? Value { get; set; }

    [MemoryPackIgnore] public ErrorCodes? InternalErrorCode { get; set; }

    public string? Message { get; private set; }

    public static Result<T> Success(T value)
    {
        return new Result<T> { IsSuccess = true, Message = "Success", Value = value };
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

    [MemoryPackIgnore] public ErrorCodes? InternalErrorCode { get; set; }

    public string? Message { get; private set; }

    public static Result Success() => new Result { IsSuccess = true, Message = "Success" };

    public static Result Failure(ErrorCodes errorCode, string message) => new Result
        { IsSuccess = false, InternalErrorCode = errorCode, Message = message };
}