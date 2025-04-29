using System;
using System.IO;
using UnityEngine;
using System.Net.Sockets;

public class Test : MonoBehaviour
{
    private TcpClient client;
    private NetworkStream stream;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        StartAsync();
    }

    private async void StartAsync()
    {
        try
        {
            client = new TcpClient();
            await client.ConnectAsync("127.0.0.1", 4242);
            
            stream = client.GetStream();
            
            var chat = new ChatPacket
            {
                sender = "UnityClient",
                receiver = "ALL",
                message = "Hello Server!",
                chatType = ChatType.GLOBAL
            };
            
            var body = chat.Serialize();
            var header = new PacketHeader
            {
                type = PacketType.CHAT,
                size = (uint)body.Length
            };
            var headerBytes = PacketHeader.Serialize(header);
            var fullPacket = new byte[headerBytes.Length + body.Length];
            Buffer.BlockCopy(headerBytes, 0, fullPacket, 0, headerBytes.Length);
            Buffer.BlockCopy(body, 0, fullPacket, headerBytes.Length, body.Length);
            await stream.WriteAsync(fullPacket,0,fullPacket.Length);
            Debug.Log("패킷 전송 완료");
        }
        catch (Exception ex)
        {
            Debug.LogError("서버 연결 실패: " + ex.Message);
        }
    }
}
