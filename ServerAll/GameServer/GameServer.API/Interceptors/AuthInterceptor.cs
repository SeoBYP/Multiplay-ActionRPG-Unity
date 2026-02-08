using GameServer.Application.Services.Auth.Interfaces;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.AspNetCore.Authorization;

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
        logger.LogInformation($"gRPC 요청: {context.Method}");

        // Step 1: [AllowAnonymous] 체크
        var httpContext = context.GetHttpContext();
        var endpoint = httpContext.GetEndpoint();
        var allowAnonymous = endpoint?.Metadata.GetMetadata<IAllowAnonymous>() != null;

        if (allowAnonymous)
        {
            logger.LogInformation($"{methodName}은 인증 불필요");
            return await continuation(request, context);
        }

        // Step 2: JWT 추출
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
            logger.LogWarning($"잘못된 Authorization 형식: {authHeader}");
            throw new RpcException(new Status(
                StatusCode.Unauthenticated,
                "Invalid authorization format"));
        }

        var token = authHeader.Substring("Bearer ".Length).Trim();

        // Step 3: JWT 검증
        var isValid = await authService.ValidateTokenAsync(token);
        
        if (!isValid)
        {
            logger.LogWarning("토큰 검증 실패");
            throw new RpcException(new Status(
                StatusCode.Unauthenticated,
                "Invalid or expired token"));
        }

        logger.LogInformation("인증 성공");

        // Step 4: 다음 단계 실행
        return await continuation(request, context);
    }
}