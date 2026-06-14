using System.Threading;
using Cysharp.Threading.Tasks;

namespace Game.System.Progression
{
    /// <summary>
    /// 진행(레벨/경험치/스탯) 조회 오케스트레이션. gRPC 결과를 도메인 DTO로 정규화한다(proto 은닉).
    /// </summary>
    public interface IProgressionService
    {
        /// <summary>현재 유저(토큰)의 진행 + 레벨 파생 스탯을 조회한다. proto 는 여기서 숨긴다(Presentation 비노출).</summary>
        UniTask<(ProgressionResult Result, ProgressionData Data)> GetProgressionAsync(CancellationToken ct = default);
    }

    public enum ProgressionResult
    {
        Success,
        Failed,
    }
}
