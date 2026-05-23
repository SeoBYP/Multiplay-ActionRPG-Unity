namespace Script.System.Auth
{
    /// <summary>
    /// 서버 로그인 응답에서 받은 유저 프로필 정보를 보관하는 싱글톤.
    /// 토큰과 분리하여 AuthSession은 인증 상태만, UserProfile은 표시용 데이터만 담당한다.
    /// </summary>
    public class UserProfile
    {
        public string NickName  { get; private set; } = string.Empty;
        public string PublicId  { get; private set; } = string.Empty;
        public bool   HasProfile => !string.IsNullOrEmpty(PublicId);

        public void Set(GameServer.Grpc.User.UserInfo info)
        {
            NickName = info.NickName;
            PublicId = info.PublicId;
        }
    }
}
