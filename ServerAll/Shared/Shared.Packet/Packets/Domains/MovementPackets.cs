using MemoryPack;

namespace Shared.Packet.Packets;

/// <summary>
/// 이동 통지(클라→서버). 위치·회전은 <b>클라 권위</b>이고 서버는 릴레이·기록만 한다.
///
/// <see cref="AnimState"/> = 보낸 클라의 로코모션 FSM 상태(0=Ground/1=Jump/2=Fall/3=Land/4=Climb,
/// 진실원 = 클라 <c>Game.Gameplay.Character.StateKind</c>). <b>서버는 해석하지 않는다</b> — 연출은 클라 권위라
/// 불투명 byte 로 실어 나른다. 이게 없으면 원격은 y 변화만 보고 점프·낙하·사다리를 구분할 수 없다
/// (8방향은 위치·회전에서 역산되므로 추가 필드가 필요 없다).
/// 기본값 0 = Ground 라 값을 안 싣는 경로도 기존 동작 그대로다.
/// </summary>
[MemoryPackable]
public partial class C_Move : Packet
{
    public float PosX { get; set; }
    public float PosY { get; set; }
    public float PosZ { get; set; }
    public float RotY { get; set; }
    public long TimeStamp { get; set; }
    public byte AnimState { get; set; }
}

/// <summary>이동 브로드캐스트(서버→방). <see cref="AnimState"/> 는 <see cref="C_Move"/> 값 그대로 릴레이한다.</summary>
[MemoryPackable]
public partial class S_Move : Packet
{
    public long UserId { get; set; }
    public float PosX { get; set; }
    public float PosY { get; set; }
    public float PosZ { get; set; }
    public float RotY { get; set; }
    public long TimeStamp { get; set; }
    public byte AnimState { get; set; }
}
