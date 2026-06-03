using MemoryPack;

namespace Shared.Packet.Packets;

/// <summary>
/// 서버 → 클라: GameplayEffect(버프/디버프) 부여.
/// 남은시간은 보내지 않는다 — 권위 StartTick + 공유 카탈로그 EffectId로 양측이 duration/modifier를 산출한다.
/// </summary>
[MemoryPackable]
public partial class S_ApplyEffect : Packet
{
    public int InstanceId { get; set; }   // 고유 인스턴스 (S_RemoveEffect 타겟)
    public string EffectId { get; set; } = string.Empty; // 공유 GameplayEffectCatalog 키
    public long TargetId { get; set; }    // 효과 대상 actor
    public long SourceId { get; set; }    // 시전자 actor
    public long StartTick { get; set; }   // 권위 시작시각 (공유 시계 기준 ms)
    public int Stacks { get; set; }
}

/// <summary>
/// 서버 → 클라: 활성 Effect 제거(만료/해제). 만료의 권위는 서버.
/// </summary>
[MemoryPackable]
public partial class S_RemoveEffect : Packet
{
    public int InstanceId { get; set; }
}
