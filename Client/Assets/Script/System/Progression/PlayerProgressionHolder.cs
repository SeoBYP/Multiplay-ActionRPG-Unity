using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.System.Auth;
using VContainer.Unity;

namespace Game.System.Progression
{
    /// <summary>
    /// Main(클라 권위 로컬 전투)용 진행/스탯 캐시 홀더. 서버 GetProgression 결과를 보관해
    /// LocalCombat(데미지=AttackPower)·킬 후 레벨/Exp 로그가 동기적으로 읽는다.
    ///
    /// 진실원 = 서버(GetProgression). 홀더는 마지막 pull 값을 캐시할 뿐 — 권위 계산을 하지 않는다.
    /// (장비/버프(3.2) 합류 시 서버 GetStatsAsync 합산이 그대로 이 stats 로 흘러온다.)
    /// 갱신 시점: Main 진입(로그인 직후) 1회(StartAsync) + 킬마다(레벨업 가능, MainMonsterSpawner 가 RefreshAsync).
    /// Presentation ProgressionModel(스탯창 MVI)과 별개 — Gameplay 는 Presentation 을 참조하지 않으므로 System 에 둔다.
    /// </summary>
    public sealed class PlayerProgressionHolder : IAsyncStartable, IDisposable
    {
        private readonly IProgressionService _service;
        private readonly AuthSession _authSession;
        private readonly CancellationTokenSource _cts = new();

        /// <summary>마지막으로 pull 한 진행/스탯. 미갱신 시 default(스탯 0).</summary>
        public ProgressionData Current { get; private set; }

        /// <summary>Current 가 갱신될 때마다 발행(로그인·킬 후). HUD(InGameModel)가 구독해 exp 게이지에 중계.</summary>
        public event Action OnChanged;

        public PlayerProgressionHolder(IProgressionService service, AuthSession authSession = null)
        {
            _service = service;
            _authSession = authSession;
        }

        // 편의 접근자 — 미갱신(default) 시 0. LocalCombat MeleeDamage(base,0,0)=base 폴백 안전.
        public int Level       => Current.Level;
        public int AttackPower => Current.Stats.AttackPower;
        public int Defense     => Current.Stats.Defense;

        /// <summary>Main/던전 진입 초기 1회 pull. 인증 완료를 기다린 뒤 호출 — 인증 전 eager pull 의 Unauthenticated 노이즈 방지.</summary>
        public async UniTask StartAsync(CancellationToken ct)
        {
            if (_authSession != null)
            {
                try
                {
                    using var linked = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, ct);
                    await _authSession.AuthenticatedAsync().AttachExternalCancellation(linked.Token);
                }
                catch (OperationCanceledException)
                {
                    return; // 씬 종료/Dispose 또는 미로그인 — pull 생략
                }
            }
            await RefreshAsync(ct);
        }

        /// <summary>서버에서 진행/스탯을 다시 읽어 캐시한다(로그인·킬 후). 실패하면 직전 캐시 유지.</summary>
        public async UniTask RefreshAsync(CancellationToken ct = default)
        {
            try
            {
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, ct);
                var (result, data) = await _service.GetProgressionAsync(linked.Token);
                if (result == ProgressionResult.Success)
                {
                    Current = data;
                    OnChanged?.Invoke();
                }
            }
            catch (OperationCanceledException)
            {
                // 씬 종료/Dispose — 정상 취소
            }
        }

        public void Dispose()
        {
            _cts.Cancel();
            _cts.Dispose();
        }
    }
}
