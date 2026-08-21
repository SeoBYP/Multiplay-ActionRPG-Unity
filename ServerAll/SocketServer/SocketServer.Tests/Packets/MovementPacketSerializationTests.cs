using MemoryPack;
using Shared.Packet.Packets;

namespace Server.Tests.Packets;

/// <summary>
/// 던전 원격 애니메이션 동기화(B안) — 이동/회피 패킷의 <b>연출 상태</b> 필드 직렬화 계약.
///
/// <b>왜 상태를 싣는가</b>: 위치·회전만으로는 원격의 로코모션 <b>모드</b>를 복원할 수 없다.
/// 8방향(MoveX/MoveY)은 속도와 RotY 로 역산되지만, 점프·낙하·사다리는 모두 y 가 오르내려 구분이 불가능하다.
/// 그래서 로컬 FSM 의 상태를 1바이트로 실어 보낸다.
///
/// <b>서버는 이 값을 해석하지 않는다</b> — 릴레이만 한다(연출은 클라 권위, authority-model).
/// 그래서 서버에는 enum 을 두지 않고 <b>불투명 byte</b> 로 다룬다. 값의 의미(진실원)는 클라
/// <c>Game.Gameplay.Character.StateKind</c> 이고, 매핑은 클라 EditMode 테스트가 고정한다.
/// Union ID 는 그대로(1500/1501) — 필드 추가만이므로 새 패킷이 아니다.
/// </summary>
public class MovementPacketSerializationTests
{
    private const byte Ground = 0;
    private const byte Jump = 1;
    private const byte Climb = 4;

    [Fact]
    public void C_Move_라운드트립시_AnimState가_보존된다()
    {
        var origin = new C_Move
        {
            PosX = 1.5f, PosY = 2.5f, PosZ = -3.5f, RotY = 90f,
            TimeStamp = 1234567890L,
            AnimState = Climb,
        };

        byte[] bytes = MemoryPackSerializer.Serialize<Packet>(origin);
        var decoded = MemoryPackSerializer.Deserialize<Packet>(bytes) as C_Move;

        Assert.NotNull(decoded);
        Assert.Equal(Climb, decoded!.AnimState);
        Assert.Equal(1.5f, decoded.PosX);
        Assert.Equal(90f, decoded.RotY);
        Assert.Equal(1234567890L, decoded.TimeStamp);
    }

    [Fact]
    public void S_Move_라운드트립시_AnimState가_보존된다()
    {
        var origin = new S_Move
        {
            UserId = 42, PosX = 0f, PosY = 0f, PosZ = 0f, RotY = 0f,
            TimeStamp = 1L,
            AnimState = Jump,
        };

        byte[] bytes = MemoryPackSerializer.Serialize<Packet>(origin);
        var decoded = MemoryPackSerializer.Deserialize<Packet>(bytes) as S_Move;

        Assert.NotNull(decoded);
        Assert.Equal(42L, decoded!.UserId);
        Assert.Equal(Jump, decoded.AnimState);
    }

    [Fact]
    public void AnimState_기본값은_Ground다()
    {
        // 하위호환: 상태를 싣지 않는 경로(구 클라·기존 테스트 픽스처)는 0 = Ground 로 읽혀
        // 지금과 동일한 지상 로코모션이 재생된다(회귀 없음).
        var decoded = MemoryPackSerializer.Deserialize<Packet>(
            MemoryPackSerializer.Serialize<Packet>(new S_Move { UserId = 1 })) as S_Move;

        Assert.NotNull(decoded);
        Assert.Equal(Ground, decoded!.AnimState);
    }

    [Fact]
    public void S_Dodge_라운드트립시_회피방향이_보존된다()
    {
        // 회피 방향은 이동 패킷이 아니라 <b>회피 이벤트</b>에 싣는다 — 1회성 신호라 상시 스트림에 넣을 이유가 없다.
        var origin = new S_Dodge { UserId = 7, DirX = -1f, DirY = 0f };

        byte[] bytes = MemoryPackSerializer.Serialize<Packet>(origin);
        var decoded = MemoryPackSerializer.Deserialize<Packet>(bytes) as S_Dodge;

        Assert.NotNull(decoded);
        Assert.Equal(7L, decoded!.UserId);
        Assert.Equal(-1f, decoded.DirX);
        Assert.Equal(0f, decoded.DirY);
    }

    [Fact]
    public void C_Dodge_라운드트립시_회피방향이_보존된다()
    {
        var origin = new C_Dodge { DirX = 0f, DirY = -1f }; // 뒤구르기

        byte[] bytes = MemoryPackSerializer.Serialize<Packet>(origin);
        var decoded = MemoryPackSerializer.Deserialize<Packet>(bytes) as C_Dodge;

        Assert.NotNull(decoded);
        Assert.Equal(0f, decoded!.DirX);
        Assert.Equal(-1f, decoded.DirY);
    }
    [Fact]
    public void 서버는_AnimState를_해석하지_않고_그대로_릴레이한다()
    {
        // 연출은 클라 권위(authority-model) — 서버가 상태를 판정·보정하면 클라 FSM 과 두 진실원이 생긴다.
        var incoming = new C_Move
        {
            PosX = 5f, PosY = 0.2f, PosZ = -1f, RotY = 180f,
            TimeStamp = 999L,
            AnimState = 200, // 서버가 모르는 값이어도 그대로 나가야 한다
        };

        var broadcast = global::Server.PacketHandler.Handler.MovementHandler.BuildBroadcast(123, incoming);

        Assert.Equal(123L, broadcast.UserId);
        Assert.Equal((byte)200, broadcast.AnimState);
        Assert.Equal(5f, broadcast.PosX);
        Assert.Equal(180f, broadcast.RotY);
        Assert.Equal(999L, broadcast.TimeStamp);
    }
}
