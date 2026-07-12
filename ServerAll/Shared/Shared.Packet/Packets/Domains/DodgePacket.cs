using MemoryPack;

namespace Shared.Packet.Packets;

/// <summary>
/// 회피(Dodge) 발동 통지(클라→서버). 서버는 쿨다운을 검증하고(영구 무적 치팅 차단) 통과 시
/// 해당 플레이어에 무적 창(DodgeConfig.IframeMs)을 부여 → TickMonsters 가 그 동안 피해를 무시한다.
/// 대시 이동/위치는 기존 C_Move 로 동기화되므로 방향 payload 불필요(시간 창만 서버 권위).
/// </summary>
[MemoryPackable]
public partial class C_Dodge : Packet
{
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
}
