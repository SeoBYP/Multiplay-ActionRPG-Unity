namespace GameServer.Application.Domains.DungeonLobby;

/// <summary>
/// 유령 방 정리(리퍼) 정책.
/// </summary>
public class DungeonRoomReaperOptions
{
    /// <summary>리퍼가 도는 주기.</summary>
    public TimeSpan Interval { get; init; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// 세션 활성 만료 시각이 이만큼 지난 플레이어를 "조용하다"고 본다.
    /// </summary>
    /// <remarks>
    /// 이 신호는 하트비트가 아니라 "최근 인증 RPC + AccessToken 수명"의 근사값이라
    /// 해상도가 거칠다. 살아 있는 방을 끊는 쪽이 훨씬 나쁜 실패라서 유예를 넉넉히 잡는다.
    /// 정식 하트비트가 생기면 이 값을 줄일 수 있다.
    /// </remarks>
    public TimeSpan Grace { get; init; } = TimeSpan.FromHours(2);
}
