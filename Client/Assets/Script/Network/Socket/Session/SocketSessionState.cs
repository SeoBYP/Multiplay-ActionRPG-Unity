namespace Game.Network.Socket
{
    public enum SocketSessionState
    {
        Idle, // 대기
        Connecting, // 연결중
        Connected, // 연결됨
        Authenticating, // 인증중
        Authenticated, // 인증됨
        Joining, // 참가중
        Joined, // 참가됨
        Disconnected, // 연결 해제됨
        Failed // 실패
    }
}