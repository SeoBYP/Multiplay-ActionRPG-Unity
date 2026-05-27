namespace Game.Network.Socket
{
    public enum SocketSessionState
    {
        Idle,          // 아직 연결을 시작하지 않은 대기 상태.
        Connecting,    // 서버와 TCP 연결을 수립 중인 상태.
        Connected,     // 연결이 수립된 상태. C_PlayerJoin 전송 가능.
        Joining,       // C_PlayerJoin을 보낸 뒤 응답을 기다리는 상태.
        Joined,        // 방 참가까지 완료되어 게임 패킷 송수신이 가능한 상태.
        Disconnected,  // 정상 종료 또는 원격 종료로 연결이 끊긴 상태.
        Failed         // 예외 또는 서버 실패 응답으로 세션이 비정상 종료된 상태.
    }
}
