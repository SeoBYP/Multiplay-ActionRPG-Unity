using DotNetEnv;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using GameServer.API.Installers;
using GameServer.API.Installers.Domain;

var builder = WebApplication.CreateBuilder(args);

// 1) Environment Variables
var envPaths = new[]
{
    Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..\\..\\..\\..\\..\\Infra\\.env")),
    Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..\\..\\Infra\\.env")),
    Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..\\..\\Infra\\.env"))
};

foreach (var path in envPaths)
{
    if (File.Exists(path))
    {
        Env.Load(path);
        builder.Configuration.AddEnvironmentVariables();
        break;
    }
}

// 2) Kestrel endpoints / protocols
builder.WebHost.ConfigureKestrel(options =>
{
    // REST/Swagger/SignalR: HTTP/1.1
    options.ListenLocalhost(5131, listen => listen.Protocols = HttpProtocols.Http1);

    // gRPC: HTTP/2 (plaintext)
    options.ListenLocalhost(5132, listen => listen.Protocols = HttpProtocols.Http2);
});

// 3) Services
var serviceInstaller = new ServiceInstaller();
serviceInstaller.Install(builder.Services, builder.Configuration);

// Domain DI registrations
new UserInstaller().Install(builder.Services, builder.Configuration);
new AuthInstaller().Install(builder.Services, builder.Configuration);
new DungeonInstaller().Install(builder.Services, builder.Configuration);
new ChatInstaller().Install(builder.Services, builder.Configuration);

var app = builder.Build();

// 4) Middleware pipeline
var middlewareInstaller = new MiddlewareInstaller();
middlewareInstaller.Install(app);

app.Run();
