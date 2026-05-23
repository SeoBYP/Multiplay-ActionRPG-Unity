namespace Script.System.Startup
{
    /// <summary>
    /// 로그인 시 서버가 "이미 방에 있음"을 알려줬을 때 큐에 넣는 인텐트.
    /// LobbyViewController가 이를 소비해 방 상세 화면을 자동으로 연다.
    /// </summary>
    public sealed class RestoreRoomStartupIntent : StartupIntent
    {
        public readonly long RoomId;

        public RestoreRoomStartupIntent(long roomId) => RoomId = roomId;
    }
}
