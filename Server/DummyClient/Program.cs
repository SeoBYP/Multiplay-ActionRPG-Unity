using System;
using System.Net;
using System.Net.Sockets;
using Google.Protobuf;
using ServerCore.Protocol;

namespace DummyClient
{
    internal class Program
    {
        private static string IpAddress = "127.0.0.1";
        private static int Port = 7777;

        static async Task Main(string[] args)
        {
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            var endpoint = new IPEndPoint(IPAddress.Parse(IpAddress), Port);

            try
            {
                socket.Connect(endpoint);
                Console.WriteLine($"Connected: {endpoint}");

                // 수신 태스크 시작 (백그라운드에서 계속 받기)
                var receiveTask = ReceiveLoopAsync(socket);

                // 송신 루프 (사용자 입력)
                while (socket.Connected)
                {
                    Console.Write("> ");
                    var input = Console.ReadLine();

                    if (input == null || input.Equals("/quit", StringComparison.OrdinalIgnoreCase))
                        break;

                    if (input.Length == 0)
                        continue;

                    // 메시지 전송
                    await SendChatAsync(socket, input);
                }

                socket.Shutdown(SocketShutdown.Both);
                socket.Close();
                
                // 수신 태스크 종료 대기
                await receiveTask;
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error: {e.Message}");
            }
            finally
            {
                Console.WriteLine("Disconnected");
            }
        }

        /// <summary>
        /// 채팅 메시지 전송
        /// 
        /// 구조: [4 bytes: Length][Protobuf Packet with C_Chat]
        /// </summary>
        static async Task SendChatAsync(Socket socket, string message)
        {
            try
            {
                // Packet 생성 및 C_Chat 설정
                var packet = new Packet();
                packet.CChat = new C_Chat { Message = message };
                
                // Protobuf 직렬화
                byte[] protobufData = packet.ToByteArray();
                
                // Length 계산 및 직렬화
                int length = protobufData.Length;
                byte[] lengthBytes = BitConverter.GetBytes(length);
                
                // [Length][Protobuf] 합치기
                byte[] finalPacket = new byte[4 + length];
                Array.Copy(lengthBytes, 0, finalPacket, 0, 4);
                Array.Copy(protobufData, 0, finalPacket, 4, length);
                
                // 전송
                int offset = 0;
                while (offset < finalPacket.Length)
                {
                    int sent = await socket.SendAsync(
                        new ArraySegment<byte>(finalPacket, offset, finalPacket.Length - offset),
                        SocketFlags.None);
                    
                    if (sent == 0)
                        throw new SocketException((int)SocketError.ConnectionReset);
                    
                    offset += sent;
                }
                
                Console.WriteLine($"Sent: {message}");
            }
            catch (Exception e)
            {
                Console.WriteLine($"Send failed: {e.Message}");
            }
        }

        /// <summary>
        /// 수신 루프 (백그라운드 실행)
        /// 
        /// 서버에서 오는 메시지를 계속 받아서 처리
        /// </summary>
        static async Task ReceiveLoopAsync(Socket socket)
        {
            try
            {
                while (socket.Connected)
                {
                    // Length 읽기 (4 bytes)
                    byte[] lengthBytes = await ReceiveExactAsync(socket, 4);
                    int length = BitConverter.ToInt32(lengthBytes, 0);
                    
                    // 비정상 패킷 차단
                    if (length <= 0 || length > 65536)
                    {
                        Console.WriteLine($"Invalid packet length: {length}");
                        break;
                    }
                    
                    // Protobuf 데이터 읽기
                    byte[] protobufData = await ReceiveExactAsync(socket, length);
                    
                    // Packet 파싱
                    var packet = Packet.Parser.ParseFrom(protobufData);
                    
                    // 패킷 처리
                    HandlePacket(packet);
                }
            }
            catch (SocketException)
            {
                // 연결 종료 시 정상 종료
            }
            catch (Exception e)
            {
                Console.WriteLine($"Receive error: {e.Message}");
            }
        }

        /// <summary>
        /// 정확히 count만큼 바이트를 받을 때까지 반복
        /// 
        /// Session.cs의 ReceiveExactAsync와 동일한 로직
        /// </summary>
        static async Task<byte[]> ReceiveExactAsync(Socket socket, int count)
        {
            byte[] buffer = new byte[count];
            int offset = 0;
            
            while (offset < count)
            {
                int received = await socket.ReceiveAsync(
                    new ArraySegment<byte>(buffer, offset, count - offset),
                    SocketFlags.None);
                
                if (received == 0)
                    throw new SocketException((int)SocketError.ConnectionReset);
                
                offset += received;
            }
            
            return buffer;
        }

        /// <summary>
        /// 수신한 패킷 타입별 처리
        /// </summary>
        static void HandlePacket(Packet packet)
        {
            switch (packet.PayloadCase)
            {
                case Packet.PayloadOneofCase.SChat:
                    Console.WriteLine($"[{packet.SChat.SenderId}] {packet.SChat.Message}");
                    break;
                    
                case Packet.PayloadOneofCase.CChat:
                    Console.WriteLine($"C_Chat received (unexpected)");
                    break;
                    
                default:
                    Console.WriteLine($"Unknown packet: {packet.PayloadCase}");
                    break;
            }
        }
    }
}