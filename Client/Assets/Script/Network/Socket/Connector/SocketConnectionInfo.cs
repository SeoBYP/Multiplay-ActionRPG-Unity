namespace Game.Network.Socket
{
    /// <summary>
    /// 소켓 세션이 연결과 인증, 방 참가에 필요한 최소 정보를 담는 DTO.
    /// </summary>
    public sealed class SocketConnectionInfo
    {
        public string Host { get; }
        public int Port { get; }
        public long RoomId { get; }
        public long UserId { get; }

        /// <summary>
        /// 연결 대상 서버와 현재 유저/방 정보를 함께 초기화한다.
        /// </summary>
        public SocketConnectionInfo(string host, int port, long roomId, long userId)
        {
            Host = host;
            Port = port;
            RoomId = roomId;
            UserId = userId;
        }
    }
}
