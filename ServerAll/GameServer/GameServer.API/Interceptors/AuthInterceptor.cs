using GameServer.Application.Domains.Auth.Interfaces;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.AspNetCore.Authorization;
using Serilog.Context;

namespace GameServer.API.Interceptors;

public class AuthInterceptor(
    IAuthService authService,
    ILogger<AuthInterceptor> logger)
    : Interceptor
{
    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        var methodName = context.Method.Split('/').Last();
        
        // 클라가 보낸 TraceId 를 재사용하고 없으면 생성
        var traceId = context.RequestHeaders.GetValue("x-trace-id")
                      ?? Guid.NewGuid().ToString("N")[..8];
        
        using (LogContext.PushProperty("TraceId", traceId))
        using (LogContext.PushProperty("Method", methodName))
        {
            context.GetHttpContext().Items["TraceId"] = traceId;
            
            logger.LogInformation("gRPC 요청: {Method}", context.Method);

            var httpContext = context.GetHttpContext();
            var endpoint = httpContext.GetEndpoint();
            var allowAnonymous = endpoint?.Metadata.GetMetadata<IAllowAnonymous>() != null;

            if (allowAnonymous)
            {
                logger.LogInformation("{MethodName}은 인증 불필요", methodName);
                return await continuation(request, context);
            }

            var authHeader = context.RequestHeaders.GetValue("authorization");
        
            if (string.IsNullOrEmpty(authHeader))
            {
                logger.LogWarning("Authorization 헤더 없음");
                throw new RpcException(new Status(
                    StatusCode.Unauthenticated,
                    "Authorization header is missing"));
            }

            if (!authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                logger.LogWarning("잘못된 Authorization 형식"); 
                throw new RpcException(new Status(
                    StatusCode.Unauthenticated,
                    "Invalid authorization format"));
            }

            var token = authHeader.Substring("Bearer ".Length).Trim();

            var isValid = await authService.ValidateTokenAsync(token);
        
            if (!isValid)
            {
                logger.LogWarning("토큰 검증 실패");
                throw new RpcException(new Status(
                    StatusCode.Unauthenticated,
                    "Invalid or expired token"));
            }

            logger.LogInformation("인증 성공");

            return await continuation(request, context);
        }
    }
}