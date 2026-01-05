using System;
using System.Net;
using System.Net.Sockets;
using ServerCore;

namespace Server
{
    internal class Program
    {
        private static string IpAddress = "127.0.0.1";
        private static int Port = 7777;
        static void Main(string[] args)
        {
            SessionManager manager = new SessionManager();
            var listener = new TcpNetworkListener(IpAddress, Port,manager);
            listener.Start();
            
            while (true)
            {
                // var client = listener.AcceptSocket();
                //
                //
                // var buffer = new byte[1024];
                // var received = client.Receive(buffer);
                // var clientMessage = System.Text.Encoding.ASCII.GetString(buffer,0,received);
                // Console.WriteLine($"Received {clientMessage} from client");
                //
                // var message = $"Hello, {clientMessage}!";
                // client.Send(System.Text.Encoding.ASCII.GetBytes(message));
                // Console.WriteLine($"Sent {message} to client");
            }
        }
    }
}