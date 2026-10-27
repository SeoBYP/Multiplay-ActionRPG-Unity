namespace Game.Network.Socket
{
    /// <summary>
    /// 소켓 연결과 방 참가(C_PlayerJoin)에 필요한 최소 정보를 담는 DTO.
    /// </summary>
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
