namespace GameServer.Application.Domains.Progression.Interfaces;

/// <summary>
/// 진행(경험치) 도메인 서비스. 던전 보상 지급 등 상위 흐름이 호출한다.
/// (지금은 경험치 적립만. 레벨업 산식/스탯 성장은 후속 M5.)
/// </summary>
public interface IProgressionService
{
    /// <summary>한 유저에게 경험치를 적립한다(0 이하는 무시). 갱신된 누적 Exp 를 반환.</summary>
    Task<long> AddExpAsync(long userId, long amount, CancellationToken ct = default);
}
