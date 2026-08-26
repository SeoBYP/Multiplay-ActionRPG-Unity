using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Sinks.Graylog;
using Serilog.Sinks.Graylog.Core.Transport;
using Server.Consumer;
using Server.Infrastructure;
using Server.PacketHandler;
using Server.Room;
using StackExchange.Redis;

namespace Server
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            var graylogHost = Environment.GetEnvironmentVariable("GRAYLOG_HOST") ?? "127.0.0.1";

            var host = Host.CreateDefaultBuilder(args)
                .UseSerilog((ctx, loggerConfig) =>
                {
                    loggerConfig
                        .ReadFrom.Configuration(ctx.Configuration)
                        .Enrich.FromLogContext()
                        .WriteTo.Console(outputTemplate:
                            "[{Timestamp:HH:mm:ss} {Level:u3}] TraceId={TraceId} SessionId={SessionId} {Message:lj}{NewLine}{Exception}")
                        .WriteTo.File("logs/socketserver-.log", rollingInterval: RollingInterval.Day)
                        .WriteTo.Graylog(new GraylogSinkOptions
                        {
                            HostnameOrAddress = graylogHost,
                            Port = 12201,
                            TransportType = TransportType.Udp
                        });
                })
                .ConfigureServices((ctx, services) =>
                {
                    // 1. 설정
                    services.Configure<ServerOptions>(ctx.Configuration.GetSection("Server"));

                    // 2. 인프라 (Redis)
                    var redisConnStr = ctx.Configuration.GetConnectionString("Redis") ?? "localhost:6379,abortConnect=false";
                    services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConnStr));
                    services.AddSingleton<IDatabase>(sp =>
                        sp.GetRequiredService<IConnectionMultiplexer>().GetDatabase());

                    // 3. 메시지 큐
                    services.AddSingleton<GameStartRequestedMessageQueue>();
                    services.AddSingleton<GameSessionReadyMessageQueue>();
                    // 소모품 소비 통지(GameServer→Socket, 서버 권위 회복).
                    services.AddSingleton<Shared.Infrastructure.MessageQueue.PlayerConsumedMessageQueue>();

                    // 4. 패킷 핸들러 및 디스패처
                    services.AddSingleton<PacketHandlerRegistry>(sp =>
                    {
                        var logger = sp.GetRequiredService<ILogger<PacketHandlerRegistry>>();
                        return PacketHandlerRegistry.Build(logger);
                    });
                    services.AddSingleton<PacketDispatcher>(sp =>
                    {
                        var registry = sp.GetRequiredService<PacketHandlerRegistry>();
                        var logger = sp.GetRequiredService<ILogger<PacketDispatcher>>();
                        return registry.CreateDispatcher(logger);
                    });

                    // 5. 게임 로직 및 네트워크
                    services.AddSingleton<RoomLifecycleMessageQueue>();
                    services.AddSingleton<IRoomLifecyclePublisher>(sp => sp.GetRequiredService<RoomLifecycleMessageQueue>());
                    services.AddSingleton<DungeonResultMessageQueue>();
                    services.AddSingleton<IDungeonResultPublisher>(sp => sp.GetRequiredService<DungeonResultMessageQueue>());
                    services.AddSingleton<LootPickupMessageQueue>();
                    services.AddSingleton<ILootPickupPublisher>(sp => sp.GetRequiredService<LootPickupMessageQueue>());
                    services.AddSingleton<RoomManager>();
                    services.AddSingleton<SessionManager>();
                    services.AddSingleton<TcpNetworkListener>(sp =>
                    {
                        var options = sp.GetRequiredService<IOptions<ServerOptions>>().Value;
                        var sessionManager = sp.GetRequiredService<SessionManager>();
                        var logger = sp.GetRequiredService<ILogger<TcpNetworkListener>>();
                        return new TcpNetworkListener(options.Ip, options.Port, sessionManager, logger);
                    });

                    // 6. 백그라운드 서비스 (호스팅 서비스)
                    services.AddHostedService<TcpListenerService>();
                    services.AddHostedService<GameStartRequestedConsumer>();
                    services.AddHostedService<Server.Consumer.PlayerConsumedConsumer>();
                    services.AddHostedService<HeartBeatService>();
                    services.AddHostedService<Server.Room.RoomTickService>();
                })
                .Build();

            // 전투 트레이스 배선(AC-C1a). 패킷 핸들러가 static 이라 DI 가 닿지 않아 기동 시 1회 주입한다.
            // 기본 Off — appsettings 의 Logging:LogLevel:CombatTrace 를 Debug 로 올려야 기록된다.
            Server.Diagnostics.CombatTrace.Configure(
                host.Services.GetRequiredService<ILoggerFactory>()
                    .CreateLogger(Server.Diagnostics.CombatTrace.Category));

            await host.RunAsync();
        }
    }
}
