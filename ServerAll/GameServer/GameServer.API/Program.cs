using DotNetEnv;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using GameServer.API.Installers;
using GameServer.API.Installers.Domain;
using Serilog;
using Serilog.Sinks.Graylog;
using Serilog.Sinks.Graylog.Core.Transport;

try
{
    Log.Logger = new LoggerConfiguration()
        .MinimumLevel.Information()
        .Enrich.FromLogContext()
        .Enrich.WithThreadId()
        .WriteTo.Console(outputTemplate:
            "[{Timestamp:HH:mm:ss} {Level:u3}] TraceId={TraceId} {Message:lj}{NewLine}{Exception}")
        .WriteTo.File("logs/gameserver-.log",
            rollingInterval: RollingInterval.Day,
            outputTemplate:
            "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] TraceId={TraceId} {Message:lj}{NewLine}{Exception}")
        .WriteTo.Graylog(new GraylogSinkOptions
        {
            HostnameOrAddress = "127.0.0.1",
            Port = 12201,
            TransportType = TransportType.Udp
        })
        .CreateLogger();


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
    new CommonInstaller().Install(builder.Services, builder.Configuration);
    new UserInstaller().Install(builder.Services, builder.Configuration);
    new AuthInstaller().Install(builder.Services, builder.Configuration);
    new ChatInstaller().Install(builder.Services, builder.Configuration);
    new DungeonInstaller().Install(builder.Services, builder.Configuration);

    // 기존 ILogger 대신 Serilog 사용
    builder.Host.UseSerilog();

    var app = builder.Build();

    // 4) Middleware pipeline
    var middlewareInstaller = new MiddlewareInstaller();
    middlewareInstaller.Install(app);

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application start-up failed");
}
finally
{
    Log.CloseAndFlush();
}