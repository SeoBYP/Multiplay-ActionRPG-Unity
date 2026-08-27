namespace Shared.Infrastructure.Messages;

public sealed class GameStartRequestedMessage
{
    public long RoomId { get; init; }
    public IReadOnlyList<PlayerInfo> PlayerInfos { get; init; } = [];
    public string TraceId { get; init; } = "";
    /// <summary>플레이할 맵 식별자. 스폰 레이아웃(spawn-layouts.json) 키와 대응.</summary>
    public string MapId { get; init; } = Spawn.MapIds.Default;
}

public sealed class PlayerInfo
{
    public long UserId { get; init; }
    public string Nickname { get; init; } = "";
    public int SpawnIndex { get; init; }

    /// <summary>
    /// 합산 전투 스탯(서버 권위) — GameServer 가 게임시작 시 progression+레벨테이블로 계산해 채운다.
    /// SocketServer 는 DB 접근 없이 이 "계산된 결과"를 액터의 AbilitySystemComponent 에 세팅해 전투에 쓴다(authority-model §4c).
    /// 기본 0(미설정) — SocketServer 는 0 이면 MaxHealth 를 상수로 폴백.
    /// </summary>
    public int MaxHealth { get; init; }
    public int MaxMana { get; init; }
    public int AttackPower { get; init; }
    public int Defense { get; init; }
}
