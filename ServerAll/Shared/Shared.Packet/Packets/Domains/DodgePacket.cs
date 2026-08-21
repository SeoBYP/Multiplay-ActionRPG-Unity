using MemoryPack;

namespace Shared.Packet.Packets;

/// <summary>
/// 회피(Dodge) 발동 통지(클라→서버). 서버는 쿨다운을 검증하고(영구 무적 치팅 차단) 통과 시
/// 해당 플레이어에 무적 창(DodgeConfig.IframeMs)을 부여 → TickMonsters 가 그 동안 피해를 무시한다.
/// 대시 이동/위치는 기존 C_Move 로 동기화된다(시간 창만 서버 권위).
///
/// <see cref="DirX"/>/<see cref="DirY"/> = 캐릭터 기준 회피 방향(로컬 우+/전+, 정규화). <b>연출 전용</b> —
/// 서버는 검증하지 않고 릴레이만 한다. 없으면 원격이 8방향 구르기를 늘 정면으로 근사해 재생한다.
/// </summary>
[MemoryPackable]
public partial class C_Dodge : Packet
{
    public float DirX { get; set; }
    public float DirY { get; set; }
}

/// <summary>
/// 회피 발동 브로드캐스트(서버→방 전원). 서버가 C_Dodge 를 쿨다운·마나 검증한 뒤 통과분만 방에 발행.
/// 다른 클라의 RemoteDriver 가 해당 UserId 캐릭터의 회피(구르기) 애니를 재생한다(연출 전용 — 무적/판정은 서버 권위).
/// S_Attack 과 동일 패턴.
/// </summary>
[MemoryPackable]
public partial class S_Dodge : Packet
{
    public long UserId { get; set; }
    /// <summary>회피 방향(캐릭터 기준 우+/전+). C_Dodge 값 릴레이 — 연출 전용.</summary>
    public float DirX { get; set; }
    public float DirY { get; set; }
}
