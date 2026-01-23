using MemoryPack;

namespace GameServer.Application.Common;

[MemoryPackable]
public partial class Result<T> where T : class
{
    public T? Value { get; set; }

    [MemoryPackIgnore] public ErrorCodes? InternalErrorCode { get; set; }

    public string? Message { get; set; }

    public Result()
    {
    }

    public static Result<T> Success(T value)
    {
        return new Result<T> { Value = value };
    }

    public static Result<T> Failure(ErrorCodes errorCode, string message)
    {
        return new Result<T> { InternalErrorCode = errorCode, Message = message };
    }
}

[MemoryPackable]
public partial class Result
{
    [MemoryPackIgnore] public ErrorCodes? InternalErrorCode { get; set; }

    public string? Message { get; set; }

    public static Result Success() => new Result();

    public static Result Failure(ErrorCodes errorCode, string message) => new Result
    {
        InternalErrorCode = errorCode, Message = message
    };
}