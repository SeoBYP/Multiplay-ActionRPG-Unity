namespace Shared.Infrastructure.Messages;

/// <summary>
/// GameServer → SocketServer: 플레이어가 소모품을 소비(검증·차감 완료)했음을 통지한다.
/// SocketServer 는 해당 플레이어가 던전에 있으면 서버 권위 HP 에 회복 효과를 적용하고
/// S_ApplyEffect 로 방에 브로드캐스트한다(플레이어 HP 서버 권위, authority-model §4).
///
/// 키 = UserId(roomId 아님) — GameServer 는 던전 컨텍스트를 몰라도 되고, SocketServer 가
/// userId 로 방을 조회한다. 던전 밖(Main)이면 방이 없어 자연 no-op(회복은 클라 로컬, §2 솔로).
/// EffectId == itemId 규칙 — Shared GameplayEffectCatalog 에서 회복 모디파이어 조회.
/// </summary>
public sealed class PlayerConsumedMessage
{
    /// <summary>
    /// 소비 1건의 고유 식별자(발행 측이 채운다). 회복 적용은 비멱등(+heal)이라
    /// at-least-once 재배달에서 **이중 회복**이 될 수 있다 — 이 키로 방 단위 중복을 차단한다.
    /// 빈 문자열이면(구 메시지) 중복 차단 없이 처리한다.
    /// </summary>
    public string ConsumeId { get; init; } = "";

    public long UserId { get; init; }
    public string EffectId { get; init; } = "";
    public string TraceId { get; init; } = "";
}
