namespace Game.Tests.PlayMode.E2E
{
    /// <summary>
    /// E2E 테스트용 서버 주소 설정.
    /// Docker로 띄운 서버와 통신한다.
    /// </summary>
    public static class ServerConfig
    {
        public const string GameServerGrpcAddress = "http://localhost:5132";
        public const int TimeoutSeconds = 10;
    }
}
