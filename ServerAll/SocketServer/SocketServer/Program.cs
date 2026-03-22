using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Context;
using Serilog.Extensions.Logging;
using Serilog.Sinks.Graylog;
using Serilog.Sinks.Graylog.Core.Transport;
using Server.PacketHandler;
using Server.Room;
using StackExchange.Redis;

namespace Server
{
    internal class Program
    {
        static async Task Main(string[] args)
        {

            
            // 1. 설정 로드
            var config = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            var redisConnStr = config.GetConnectionString("Redis") ?? "localhost:6379,abortConnect=false";
            var ipAddress = config["Server:Ip"] ?? "127.0.0.1";
            var port = int.Parse(config["Server:Port"] ?? "7777");

            // 2. 로깅 설정
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .Enrich.FromLogContext()
                .WriteTo.Console(outputTemplate:
                    "[{Timestamp:HH:mm:ss} {Level:u3}] TraceId={TraceId} SessionId={SessionId} {Message:lj}{NewLine}{Exception}")
                .WriteTo.File("logs/socketserver-.log", rollingInterval: RollingInterval.Day)
                .WriteTo.Graylog(new GraylogSinkOptions
                {
                    HostnameOrAddress = "127.0.0.1",
                    Port = 12201,
                    TransportType = TransportType.Udp
                })
                .CreateLogger();
            
            using var loggerFactory = new SerilogLoggerFactory(Log.Logger);
            var logger = loggerFactory.CreateLogger<Program>();

            logger.LogInformation("Starting SocketServer at {Ip}:{Port}...", ipAddress, port);

            // 3. 인프라 초기화
            var registry = PacketHandlerRegistry.Build(loggerFactory.CreateLogger<PacketHandlerRegistry>());
            var dispatcher = registry.CreateDispatcher(loggerFactory.CreateLogger<PacketDispatcher>());
            var redis = ConnectionMultiplexer.Connect(redisConnStr);
            var gameStartQueue = new GameStartMessageQueue(redis, loggerFactory.CreateLogger<GameStartMessageQueue>());
            var roomManager = new RoomManager(loggerFactory.CreateLogger<RoomManager>(), loggerFactory.CreateLogger<Server.Room.Room>());
            var sessionManager = new SessionManager(
                dispatcher,
                roomManager,
                loggerFactory.CreateLogger<SessionManager>(),
                loggerFactory.CreateLogger<Session>());
            
            var listener = new TcpNetworkListener(ipAddress, port, sessionManager, loggerFactory.CreateLogger<TcpNetworkListener>());
            listener.Start();
            
            var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                logger.LogInformation("Stopping server...");
                cts.Cancel();
            };

            // 4. 게임 시작 메시지 큐 소비 루프
            _ = Task.Run(async () =>
            {
                try
                {
                    await foreach (var msg in gameStartQueue.DequeueAllAsync(cts.Token))
                    {
                        using (LogContext.PushProperty("TraceId", msg.TraceId))
                        using (LogContext.PushProperty("RoomId", msg.RoomId))
                        {
                            roomManager.CreateRoom(msg.RoomId, msg.PlayerIds);
                            logger.LogInformation("[GameStart] RoomId={RoomId}, Players={PlayerCount}명", msg.RoomId, msg.PlayerIds.Count);
            
                            // Redis에 준비 신호 설정 → GameServer 폴링이 읽어감
                            await redis.GetDatabase().StringSetAsync(
                                $"socket:room:{msg.RoomId}:ready",
                                $"{ipAddress}:{port}",
                                TimeSpan.FromMinutes(5));
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // 정상 종료
                }
                catch (Exception e)
                {
                    logger.LogError(e, "Error in GameStartQueue loop");
                }

            }, cts.Token);
            
            try
            {
                // 메인 스레드 대기
                await Task.Delay(Timeout.Infinite, cts.Token);
            }
            catch (OperationCanceledException)
            {
                // 정상 종료
            }
            finally
            {
                // 정리
                listener.Stop();
                sessionManager.Clear();
                logger.LogInformation("✅ SocketServer stopped");
            }
        }
    }
}
