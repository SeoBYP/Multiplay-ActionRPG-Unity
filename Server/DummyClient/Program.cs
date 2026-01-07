using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Google.Protobuf;
using ServerCore.Protocol;

namespace DummyClient
{
    internal class Program
    {
        private static string IpAddress = "127.0.0.1";
        private static int Port = 7777;

        static void Main(string[] args)
        {
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            var endpoint = new IPEndPoint(IPAddress.Parse(IpAddress), Port);

            socket.Connect(endpoint);
            Console.WriteLine($"Connected: {endpoint}");

            while (socket.Connected)
            {
                Console.Write("> ");
                var input = Console.ReadLine();

                // Ctrl+Z / 입력 종료 처리
                if (input == null)
                    break;

                // 빈 문자열은 스킵(원하면 허용 가능)
                if (input.Length == 0)
                    continue;

                // 종료 명령
                if (input.Equals("/quit", StringComparison.OrdinalIgnoreCase))
                    break;

                // Send
                var msg = new C_Chat();
                msg.Message = input;
                var sendBytes = msg.ToByteArray();
                socket.Send(sendBytes);
                
                Console.WriteLine($"Sent: {C_Chat.Parser.ParseFrom(sendBytes).Message}");

                // Receive (응답 필수)
                var buffer = new byte[1024];
                int received = socket.Receive(buffer);

                if (received == 0)
                {
                    Console.WriteLine("Server disconnected (EOF).");
                    break;
                }
                
                Console.WriteLine($"[Receive] Received {received} bytes: {BitConverter.ToString(buffer, 0, received)}");

                var serverMessage = C_Chat.Parser.ParseFrom(buffer, 0, received);
                Console.WriteLine($"Received: {serverMessage.Message}");
            }

            try { socket.Shutdown(SocketShutdown.Both); } catch { }
            socket.Close();
            Console.WriteLine("Closed.");
        }
    }
}