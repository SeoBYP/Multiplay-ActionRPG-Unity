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
        
        // TraceId: 클라이언트가 보낸 것 재사용, 없으면 신규 생성
        var traceId = context.RequestHeaders.GetValue("x-trace-id")
                      ?? Guid.NewGuid().ToString("N")[..8];
        
        // 이 요청의 모든 로그에 TraceId 자동 첨부
        using (LogContext.PushProperty("TraceId", traceId))
        using (LogContext.PushProperty("Method", methodName))
        {
            context.GetHttpContext().Items["TraceId"] = traceId;
            
            // Graylog에서 Method 필드로 검색 가능
            logger.LogInformation("gRPC 요청: {Method}", context.Method);

            // Step 1: [AllowAnonymous] 체크
            var httpContext = context.GetHttpContext();
            var endpoint = httpContext.GetEndpoint();
            var allowAnonymous = endpoint?.Metadata.GetMetadata<IAllowAnonymous>() != null;

            if (allowAnonymous)
            {
                logger.LogInformation("{MethodName}은 인증 불필요", methodName);
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
                logger.LogWarning("잘못된 Authorization 형식"); 
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
}