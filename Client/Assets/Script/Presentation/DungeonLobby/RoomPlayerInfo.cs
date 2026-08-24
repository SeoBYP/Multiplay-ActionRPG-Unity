namespace Game.Presentation.DungeonLobby
{
    /// <summary>
    /// 방 플레이어 정보 도메인 타입.
    /// Game.GUI 레이어가 Network(proto) UserInfo 타입에 의존하지 않도록 Game.Presentation에 정의한다.
    /// NickName이 없으면 PublicId로 대체하는 정책을 생성자에서 처리한다.
    /// </summary>
    public sealed class RoomPlayerInfo
    {
        public readonly string PublicId;
        public readonly string NickName;

        /// <summary>이 플레이어가 방장인가. 방장은 준비 개념이 없어 <see cref="IsReady"/> 는 항상 true 로 본다.</summary>
        public readonly bool IsHost;

        /// <summary>준비 완료 상태. 방장은 시작 버튼이 곧 의사표시이므로 항상 true.</summary>
        public readonly bool IsReady;

        public RoomPlayerInfo(string publicId, string nickName, bool isHost = false, bool isReady = false)
        {
            PublicId = publicId;
            NickName = string.IsNullOrEmpty(nickName) ? publicId : nickName;
            IsHost   = isHost;
            IsReady  = isHost || isReady;
        }
    }
}
