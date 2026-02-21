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

        // endpoints
        app.MapControllers();
        
        // gRPC services
        app.MapGrpcService<AuthGrpcService>();
        app.MapGrpcService<UserGrpcService>();
        app.MapGrpcService<DungeonLobbyGrpcService>();
        app.MapGrpcService<ChatGrpcService>();
    }
}
