namespace GameServer.Domain.Entities.User;

/// <summary>
/// Main(비던전) 플레이어의 마지막 위치. 재접속 시 여기서 시작한다(B7).
///
/// ⚠ **이 좌표는 클라가 만든 값이다.** Main 은 소켓 미연결이라 서버 권위 시뮬레이션이 없다.
/// 서버는 자신이 아는 것(맵 경계, spawn-layouts 의 <c>MapBounds</c>)만 검증하고 경계 밖이면
/// 가장 가까운 저작 스폰으로 스냅한다. 이동 궤적·근접 검증은 하지 않는다 —
/// 클라가 보고한 좌표로 클라를 검증하는 것은 순환이기 때문이다(cleanup-backlog F5·B7).
///
/// 키 = user_id(지금). 캐릭터 교체 도입 시 character_id 로 이관(Progression·Inventory 와 동일). [[character-swap-direction]]
/// 회전은 Y축만 — 캐릭터는 수평 회전만 하고 피치/롤은 카메라 관심사라 지속화 대상이 아니다.
/// </summary>
public class UserPosition
{
    public long UserId { get; private set; }

    /// <summary>어느 맵의 좌표인가. 맵이 바뀌면(콘텐츠 개편) 이 값이 안 맞아 폴백된다.</summary>
    public string MapId { get; private set; } = "";

    public float X { get; private set; }
    public float Y { get; private set; }
    public float Z { get; private set; }
    public float RotY { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    private UserPosition() { }

    public static UserPosition Create(long userId, string mapId, float x, float y, float z, float rotY)
    {
        if (userId <= 0)
            throw new ArgumentException("UserId must be positive", nameof(userId));
        if (string.IsNullOrWhiteSpace(mapId))
            throw new ArgumentException("MapId is required", nameof(mapId));

        return new UserPosition
        {
            UserId = userId,
            MapId = mapId,
            X = x,
            Y = y,
            Z = z,
            RotY = rotY,
            UpdatedAt = DateTime.UtcNow,
        };
    }

    /// <summary>캐시(Redis Hash)에서 복원. UpdatedAt 은 캐시에 없으므로 의미 없음(표시 미사용).</summary>
    public static UserPosition FromRedis(long userId, string mapId, float x, float y, float z, float rotY)
        => new()
        {
            UserId = userId,
            MapId = mapId,
            X = x,
            Y = y,
            Z = z,
            RotY = rotY,
            UpdatedAt = DateTime.UtcNow,
        };

    public void Update(string mapId, float x, float y, float z, float rotY)
    {
        MapId = mapId;
        X = x;
        Y = y;
        Z = z;
        RotY = rotY;
        UpdatedAt = DateTime.UtcNow;
    }
}
