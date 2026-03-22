using System.Net;
using System.Text.Json;
using GameServer.Application.Common;

namespace GameServer.API.Middleware;

public class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger,
    IHostEnvironment env)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            // 다음 미들웨어 실행
            await next(context);
        }
        catch (Exception ex)
        {
            // 예외 로깅
            logger.LogError(ex, 
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

        var mapping = ExceptionMapper.Map(exception);
        context.Response.StatusCode = (int)mapping.HttpStatusCode;

        var response = new
        {
            error = new
            {
                message = mapping.Message,
                // 개발 환경에서만 상세 정보 제공
                detail = env.IsDevelopment() ? exception.Message : null,
                stackTrace = env.IsDevelopment() ? exception.StackTrace : null
            }
        };

        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(json);
    }
}
