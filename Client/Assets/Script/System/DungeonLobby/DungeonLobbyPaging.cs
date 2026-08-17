namespace Game.System.DungeonLobby
{
    /// <summary>
    /// 방 목록 요청의 클라 기본값(9.6).
    ///
    /// <para><b>권위는 서버다.</b> 여기 값은 "안 지정하면 이만큼 달라"는 요청 기본값일 뿐이고,
    /// 실제 상한(50)·정렬(RoomId 내림차순)은 서버 <c>DungeonLobbyPaging</c> 가 강제한다 —
    /// 클라가 큰 값을 보내도 서버가 자른다. 그래서 두 값이 어긋나도 안전하다(클라가 더 못 받을 뿐).</para>
    /// </summary>
    public static class DungeonLobbyPaging
    {
        /// <summary>한 번에 받아올 방 수. 서버 기본값과 같게 맞춰 둔다.</summary>
        public const int DefaultPageSize = 20;
    }
}
