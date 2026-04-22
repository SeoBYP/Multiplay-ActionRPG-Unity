namespace Game.Network.Socket
{
    /// <summary>
    /// 소켓 세션의 현재 진행 단계를 나타내는 상태 머신 열거형.
    /// </summary>
    public enum SocketSessionState
    {
        Idle, // 아직 연결을 시작하지 않은 대기 상태.
        Connecting, // 서버와 TCP 연결을 수립 중인 상태.
        Connected, // 연결은 되었지만 인증은 아직 수행하지 않은 상태.
        Authenticating, // 인증 패킷을 보낸 뒤 응답을 기다리는 상태.
        Authenticated, // 인증에 성공해 방 참가 요청이 가능한 상태.
        Joining, // 방 참가 요청을 보낸 뒤 응답을 기다리는 상태.
        Joined, // 방 참가까지 완료되어 게임 패킷 송수신이 가능한 상태.
        Disconnected, // 정상 종료 또는 원격 종료로 연결이 끊긴 상태.
        Failed // 예외 또는 서버 실패 응답으로 세션이 비정상 종료된 상태.
    }
}
