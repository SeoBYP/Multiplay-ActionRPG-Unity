using Server.Packet;
using Server.Room;
using StackExchange.Redis;

namespace Server
{
    internal class Program
    {
        private static string IpAddress = "127.0.0.1";
        private static int Port = 7777;
        static void Main(string[] args)
        {
            var registry = PacketHandlerRegistry.Build();
            var dispatcher = registry.CreateDispatcher();
            var redis = ConnectionMultiplexer.Connect("localhost:6379");
            var gameStartQueue = new GameStartMessageQueue(redis);
            var roomManager = new RoomManager();
            var sessionManager = new SessionManager(dispatcher);
            
            var listener = new TcpNetworkListener(IpAddress, Port, sessionManager);
            listener.Start();
            
            var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                Console.WriteLine("Stopping server...");
                cts.Cancel();
            };

            // TODO : Room 관련 로직 분기 처리
            _ = Task.Run(async () =>
            {
                try
                {
                    await foreach (var msg in gameStartQueue.DequeueAllAsync(cts.Token))
                    {
                        roomManager.CreateRoom(msg.RoomId, msg.PlayerIds);
                        Console.WriteLine($"[GameStart] RoomId={msg.RoomId}, Players={msg.PlayerIds.Count}명");
            
                        // Redis에 준비 신호 설정 → GameServer 폴링이 읽어감
                        await redis.GetDatabase().StringSetAsync(
                            $"socket:room:{msg.RoomId}:ready",
                            "127.0.0.1:7777",
                            TimeSpan.FromMinutes(5));
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }

            }, cts.Token);
            
            
            try
            {
                // 메인 스레드 대기
                Task.Delay(Timeout.Infinite, cts.Token).Wait(cts.Token);
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
                Console.WriteLine("✅ SocketServer stopped");
            }
        }
    }
}