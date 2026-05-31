namespace Game.Presentation.DungeonLobby
{
    /// <summary>
    /// 방 상태 도메인 열거형.
    /// Game.GUI 레이어가 Network(proto) 타입에 의존하지 않도록 Game.OutGame에 정의한다.
    /// </summary>
    public enum RoomStatus
    {
        Waiting  = 0,
        Starting = 1,
        Playing  = 2,
        Closed   = 3,
    }
}
