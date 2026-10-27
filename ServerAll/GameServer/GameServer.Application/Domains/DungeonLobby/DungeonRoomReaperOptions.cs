namespace GameServer.Application.Domains.DungeonLobby;

/// <summary>유령 방 정리(리퍼) 정책.</summary>
public class DungeonRoomReaperOptions
{
    public TimeSpan Interval { get; init; } = TimeSpan.FromMinutes(10);

    /// <summary>세션 활성 만료 시각이 이만큼 지난 플레이어를 "조용하다"고 본다.</summary>
    /// <remarks>
    /// 하트비트가 아니라 "최근 인증 RPC + AccessToken 수명"의 근사값이라 해상도가 거칠다.
    /// 살아 있는 방을 끊는 쪽이 더 나쁜 실패라 유예를 넉넉히 잡는다.
    /// </remarks>
    public TimeSpan Grace { get; init; } = TimeSpan.FromHours(2);
}
