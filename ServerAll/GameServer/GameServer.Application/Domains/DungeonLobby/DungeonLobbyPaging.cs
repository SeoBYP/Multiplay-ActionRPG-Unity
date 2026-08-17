namespace GameServer.Application.Domains.DungeonLobby;

/// <summary>
/// 방 목록 페이징 한계값(9.6). <b>서버가 진실원</b> — 클라가 보낸 크기를 그대로 믿지 않는다.
/// (이전엔 <c>GetRoomsRequest.room_count</c> 가 아예 무시되고 전체 활성 방이 반환됐다.)
/// </summary>
public static class DungeonLobbyPaging
{
    /// <summary>클라가 크기를 안 보냈을 때(0 이하) 쓰는 기본 페이지 크기.</summary>
    public const int DefaultPageSize = 20;

    /// <summary>페이지 크기 상한. 클라가 아무리 크게 불러도 여기서 잘린다(응답 폭주 차단).</summary>
    public const int MaxPageSize = 50;

    /// <summary>요청 크기를 유효 범위로 접는다.</summary>
    public static int ClampLimit(int limit)
        => limit <= 0 ? DefaultPageSize : Math.Min(limit, MaxPageSize);

    /// <summary>요청 오프셋을 유효 범위로 접는다.</summary>
    public static int ClampOffset(int offset) => offset < 0 ? 0 : offset;
}
