using System;
using System.Net;
using System.Net.Sockets;

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

            Thread.Sleep(500);
            
            socket.Connect(endpoint);
            
            while (socket.Connected)
            {
                var input = Console.ReadLine();
                socket.Send(System.Text.Encoding.ASCII.GetBytes(input));
                Console.WriteLine($"Sent {input} to server");
                
                var buffer = new byte[1024];
                var received = socket.Receive(buffer);
                var serverMessage = System.Text.Encoding.ASCII.GetString(buffer,0,received);
                Console.WriteLine($"Received {serverMessage} from server");
                
                socket.Shutdown(SocketShutdown.Both);
                socket.Close();
                Console.WriteLine("Disconnected");
            }
        }
    }
}