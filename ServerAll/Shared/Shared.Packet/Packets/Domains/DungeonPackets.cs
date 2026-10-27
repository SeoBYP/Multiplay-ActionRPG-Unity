using MemoryPack;

namespace Shared.Packet.Packets;

/// <summary>
/// 서버 → 클라: 던전 클리어(몬스터 전멸). 서버가 1회만 감지해 방에 브로드캐스트한다.
/// 보상 지급은 별개로 GameServer 가 DungeonClearMessage(Redis Stream)를 소비해 처리한다.
/// RewardExp = 표시용 — 지급과 같은 Shared 카탈로그(spawn-layouts.json expReward)에서 채운다.
/// </summary>
[MemoryPackable]
public partial class S_DungeonClear : Packet
{
    public long RoomId { get; set; }
    public long RewardExp { get; set; }
}

/// <summary>
/// 서버 → 클라: 던전 실패(참가자 전원 다운). 서버가 1회만 감지해 방에 브로드캐스트한다.
/// 실패는 보상이 없다(GameServer 통지 없음).
/// </summary>
[MemoryPackable]
public partial class S_DungeonFailed : Packet
{
    public long RoomId { get; set; }
}

/// <summary>
/// 클라 → 서버: 로컬 플레이어 사망(HP 0) 보고. 플레이어 HP 는 클라 권위라 서버는 이 신호로 다운을 집계하고,
/// 참가자 전원이 다운되면 S_DungeonFailed 를 발화한다.
/// </summary>
[MemoryPackable]
public partial class C_PlayerDead : Packet
{
}

/// <summary>
/// 서버 → 클라: 한 플레이어의 다운(HP 0)을 방에 브로드캐스트한다(원격 다운 가시성).
/// C_PlayerDead 수신 시 그 플레이어의 UserId 를 실어 전파한다. 전원 다운 집계(S_DungeonFailed)와는 별개.
/// </summary>
[MemoryPackable]
public partial class S_PlayerDead : Packet
{
    public long UserId { get; set; }
}
