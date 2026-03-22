using GameServer.API.Middleware;
using Grpc.Core;
using Grpc.Core.Interceptors;

namespace GameServer.API.Interceptors;

public class GrpcExceptionInterceptor(
    ILogger<GrpcExceptionInterceptor> logger,
    IHostEnvironment env) : Interceptor
{
    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        try
        {
            return await continuation(request, context);
        }
        catch (Exception ex)
        {
            throw HandleException(context, ex);
        }
    }

    public override async Task<TResponse> ClientStreamingServerHandler<TRequest, TResponse>(
        IAsyncStreamReader<TRequest> requestStream,
        ServerCallContext context,
        ClientStreamingServerMethod<TRequest, TResponse> continuation)
    {
        try
        {
            return await continuation(requestStream, context);
        }
        catch (Exception ex)
        {
            throw HandleException(context, ex);
        }
    }

    public override async Task ServerStreamingServerHandler<TRequest, TResponse>(
        TRequest request,
        IServerStreamWriter<TResponse> responseStream,
        ServerCallContext context,
        ServerStreamingServerMethod<TRequest, TResponse> continuation)
    {
        try
        {
            await continuation(request, responseStream, context);
        }
        catch (Exception ex)
        {
            throw HandleException(context, ex);
        }
    }

    public override async Task DuplexStreamingServerHandler<TRequest, TResponse>(
        IAsyncStreamReader<TRequest> requestStream,
        IServerStreamWriter<TResponse> responseStream,
        ServerCallContext context,
        DuplexStreamingServerMethod<TRequest, TResponse> continuation)
    {
        try
        {
            await continuation(requestStream, responseStream, context);
        }
        catch (Exception ex)
        {
            throw HandleException(context, ex);
        }
    }

    private Exception HandleException(ServerCallContext context, Exception exception)
    {
        if (exception is RpcException rpcException)
            return rpcException;

        if (exception is OperationCanceledException || context.CancellationToken.IsCancellationRequested)
            return new RpcException(new Status(StatusCode.Cancelled, "Request was cancelled"));

        var mapping = ExceptionMapper.Map(exception);
        logger.LogError(exception, "Unhandled gRPC exception occurred. Method: {Method}", context.Method);

        var detail = env.IsDevelopment() ? exception.Message : mapping.Message;
        return new RpcException(new Status(mapping.GrpcStatusCode, detail));
    }
}
