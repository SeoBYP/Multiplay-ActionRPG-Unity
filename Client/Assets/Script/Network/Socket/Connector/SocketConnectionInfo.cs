using UnityEngine;

namespace Game.Network.Socket
{
    public sealed class SocketConnectionInfo
    {
        public string Host { get; }
        public int Port { get; }
        public long RoomId { get; }
        public long UserId { get; }

        public SocketConnectionInfo(string host, int port, long roomId, long userId)
        {
            Host = host;
            Port = port;
            RoomId = roomId;
            UserId = userId;
        }
    }
}

