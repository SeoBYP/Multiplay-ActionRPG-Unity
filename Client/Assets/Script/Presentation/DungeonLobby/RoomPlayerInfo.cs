namespace Game.Presentation.DungeonLobby
{
    /// <summary>
    /// 방 플레이어 정보 도메인 타입.
    /// Game.GUI 레이어가 Network(proto) UserInfo 타입에 의존하지 않도록 Game.OutGame에 정의한다.
    /// NickName이 없으면 PublicId로 대체하는 정책을 생성자에서 처리한다.
    /// </summary>
    public sealed class RoomPlayerInfo
    {
        public readonly string PublicId;
        public readonly string NickName;

        public RoomPlayerInfo(string publicId, string nickName)
        {
            PublicId = publicId;
            NickName = string.IsNullOrEmpty(nickName) ? publicId : nickName;
        }
    }
}
