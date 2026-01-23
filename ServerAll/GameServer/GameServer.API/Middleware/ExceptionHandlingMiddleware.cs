using System.Net;
using System.Text.Json;
using GameServer.Application.Common;
using StackExchange.Redis;

namespace GameServer.API.Middleware;

public class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger,
    IHostEnvironment env)
{
    private readonly RequestDelegate _next = next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger = logger;
    private readonly IHostEnvironment _env = env;
    
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            // 다음 미들웨어 실행
            await _next(context);
        }
        catch (Exception ex)
        {
            // 예외 로깅
            _logger.LogError(ex, 
                "Unhandled exception occurred. Path: {Path}, Method: {Method}", 
                context.Request.Path, 
                context.Request.Method);

            // 에러 응답 반환
            await HandleExceptionAsync(context, ex);
        }
    }
    
    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        var (statusCode, message) = exception switch
        {
            // Redis 연결 오류
            RedisConnectionException => 
                (HttpStatusCode.ServiceUnavailable, ErrorMessages.ServiceUnavailable),
            
            RedisTimeoutException => 
                (HttpStatusCode.ServiceUnavailable, ErrorMessages.ServiceUnavailable),
            
            // 일반 예외
            InvalidOperationException => 
                (HttpStatusCode.BadRequest,ErrorMessages.InvalidRequest),
            
            ArgumentException => 
                (HttpStatusCode.BadRequest, ErrorMessages.InvalidRequest),
            
            UnauthorizedAccessException => 
                (HttpStatusCode.Unauthorized, ErrorMessages.Unauthorized),
            
            // 기타 모든 예외
            _ => (HttpStatusCode.InternalServerError, ErrorMessages.InvalidRequest)
        };

        context.Response.StatusCode = (int)statusCode;

        var response = new
        {
            error = new
            {
                message = message,
                // 개발 환경에서만 상세 정보 제공
                detail = _env.IsDevelopment() ? exception.Message : null,
                stackTrace = _env.IsDevelopment() ? exception.StackTrace : null
            }
        };

        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(json);
    }
}