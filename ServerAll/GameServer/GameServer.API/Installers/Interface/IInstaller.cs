namespace GameServer.API.Installers;

public interface IServiceInstaller
{
    void Install(IServiceCollection services, IConfiguration configuration);
}

public interface IMiddlewareInstaller
{
    void Install(WebApplication app);
}

public interface IInstaller
{
    void Install(WebApplicationBuilder app);
}

