using MemoryPack;

namespace Shared.Packet.Packets;

/// <summary>
/// 서버 → 클라(owner-only): 권위 마나 정정. <b>차감(스킬/회피 발동)·발동 거부·입장 초기화</b> 시점에만 전송한다.
/// 리젠(시간 비례 회복)은 매 틱 보내지 않는다 — 클라·서버가 <see cref="Script.System.GamePlayAbilitySystem.ManaConfig.RegenPerSecond"/>
/// 로 동일 예측해 수렴하므로(per-tick 스팸 회피). 클라는 이 값을 받아 로컬 ASC 의 Mana 를 권위로 덮어쓴다.
/// </summary>
[MemoryPackable]
public partial class S_PlayerMana : Packet
{
    public long UserId { get; set; }
    public int Mana { get; set; }
    public int MaxMana { get; set; }
}
