using System;
using System.Net;
using System.Net.Sockets;
using Server.Packet;
using ServerCore;

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
                Console.WriteLine("✅ Server stopped");
            }
        }
    }
}