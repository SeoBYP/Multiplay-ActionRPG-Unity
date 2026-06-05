using MemoryPack;

namespace Shared.Packet.Packets;

/// <summary>
/// 서버 → 클라: 몬스터 1마리 스폰. 입장 시 현재 몬스터 로스터 전송에도 재사용.
/// 몬스터는 서버 권위 NPC라 C_ 입력 패킷이 없다.
/// </summary>
[MemoryPackable]
public partial class S_SpawnMonster : Packet
{
    public int InstanceId { get; set; }                  // 방 내 고유 ID(서버 발급)
    public string MonsterId { get; set; } = string.Empty; // 타입 키(서버·클라 공유 식별자)
    public float PosX { get; set; }
    public float PosY { get; set; }
    public float PosZ { get; set; }
    public float RotY { get; set; }
    public int Hp { get; set; }
    public int MaxHp { get; set; }
}

/// <summary>
/// 서버 → 클라: 몬스터 주기 상태(이동/HP/페이즈). RoomTickService가 매 틱 브로드캐스트.
/// 클라는 이 스냅샷으로 위치를 보간만 한다(서버 시뮬 + 클라 보간 모델).
/// </summary>
[MemoryPackable]
public partial class S_MonsterState : Packet
{
    public int InstanceId { get; set; }
    public float PosX { get; set; }
    public float PosY { get; set; }
    public float PosZ { get; set; }
    public float RotY { get; set; }
    public int Hp { get; set; }
    public byte Phase { get; set; } // 0 Idle / 1 Patrol / 2 Chase / 3 Attack
}

/// <summary>
/// 서버 → 클라: 몬스터 사망 → 디스폰. HP 권위는 서버, 사망 판정도 서버.
/// </summary>
[MemoryPackable]
public partial class S_MonsterDead : Packet
{
    public int InstanceId { get; set; }
}
