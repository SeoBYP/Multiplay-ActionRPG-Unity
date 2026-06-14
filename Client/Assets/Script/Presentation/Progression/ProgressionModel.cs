using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.System.Progression;
using R3;

namespace Game.Presentation.Progression
{
    /// <summary>
    /// 스탯창(7.3)의 MVI Model. View 는 Refresh 만 호출하고 State 를 구독한다.
    /// GUI(스탯창 View)는 이 Model 만 주입받는다(IProgressionService·proto 비노출).
    /// 진행/스탯은 서버 권위 pull(GetProgression) — 창을 열 때마다 최신값을 가져온다.
    /// </summary>
    public sealed class ProgressionModel : IDisposable
    {
        private readonly IProgressionService _service;
        private readonly CancellationTokenSource _cts = new();

        private readonly ReactiveProperty<ProgressionViewState> _state = new(ProgressionViewState.Initial);
        public ReadOnlyReactiveProperty<ProgressionViewState> State => _state.ToReadOnlyReactiveProperty();

        public ProgressionModel(IProgressionService service)
        {
            _service = service;
        }

        /// <summary>서버에서 진행/스탯을 다시 읽어 State 로 발행한다(스탯창 열기/갱신 시).</summary>
        public void Refresh() => RefreshAsync().Forget();

        private async UniTaskVoid RefreshAsync()
        {
            _state.Value = _state.Value.WithLoading();
            try
            {
                var (result, data) = await _service.GetProgressionAsync(_cts.Token);
                _state.Value = result != ProgressionResult.Success
                    ? _state.Value.WithError(result.ToString())
                    : ProgressionViewState.Loaded(data);
            }
            catch (OperationCanceledException)
            {
                // 창 닫힘/씬 전환 — 정상 취소
            }
        }

        public void Dispose()
        {
            _cts.Cancel();
            _cts.Dispose();
            _state.Dispose();
        }
    }
}
