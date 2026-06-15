using GameServer.API.Middleware;
using GameServer.API.Services;

namespace GameServer.API.Installers;

public class MiddlewareInstaller : IMiddlewareInstaller
{
    public void Install(WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();

            // gRPC reflection for tooling (Postman, grpcurl, etc.)
            app.MapGrpcReflectionService();
        }

        app.UseMiddleware<ExceptionHandlingMiddleware>();

        app.UseResponseCompression();

        app.UseRouting();

        app.UseCors();

        app.UseAuthentication();
        app.UseAuthorization();

        // health check — Docker healthcheck 및 의존 서비스 대기용
        app.MapHealthChecks("/healthz");

        // endpoints
        app.MapControllers();
        
        // gRPC services
        app.MapGrpcService<AuthGrpcService>();
        app.MapGrpcService<UserGrpcService>();
        app.MapGrpcService<DungeonLobbyGrpcService>();
        app.MapGrpcService<ChatGrpcService>();
        app.MapGrpcService<InventoryGrpcService>();
        app.MapGrpcService<EquipmentGrpcService>();
        app.MapGrpcService<ProgressionGrpcService>();
    }
}
