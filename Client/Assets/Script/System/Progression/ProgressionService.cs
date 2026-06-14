using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Network.Https.Interfaces;
using GameServer.Grpc.Progression;
using UnityEngine;

namespace Game.System.Progression
{
    /// <summary>
    /// 진행 서비스. gRPC(ProgressionService.GetProgression) 호출 → 도메인 DTO 변환.
    /// 스탯은 서버가 레벨 테이블에서 룩업해 내려준 값을 그대로 담는다(클라는 표시만, 권위 = 서버).
    /// </summary>
    public sealed class ProgressionService : IProgressionService
    {
        private readonly IProgressionGrpcService _grpc;

        public ProgressionService(IProgressionGrpcService grpc)
        {
            _grpc = grpc;
        }

        public async UniTask<(ProgressionResult Result, ProgressionData Data)> GetProgressionAsync(CancellationToken ct = default)
        {
            try
            {
                var res = await _grpc.GetProgressionAsync(new GetProgressionRequest(), ct);
                if (!res.Result.Success)
                {
                    Debug.LogWarning($"[ProgressionService] GetProgression 실패: code={res.Result.ErrorCode}");
                    return (ProgressionResult.Failed, default);
                }

                var s = res.Stats;
                var stats = s == null
                    ? default
                    : new ProgressionStats(s.MaxHealth, s.MaxMana, s.AttackPower, s.Defense,
                        s.Strength, s.Dexterity, s.Intelligence);

                var data = new ProgressionData(res.Level, res.Exp, res.ExpToNext, res.MaxLevel, stats);
                return (ProgressionResult.Success, data);
            }
            catch (Exception e)
            {
                Debug.LogError($"[ProgressionService] GetProgression 예외: {e.Message}");
                return (ProgressionResult.Failed, default);
            }
        }
    }
}
