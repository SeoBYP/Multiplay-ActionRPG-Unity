using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Network.Socket;
using R3;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer.Unity;

namespace Game.Presentation.InGame
{
    /// <summary>
    /// 인게임 화면의 MVI Model.
    ///
    /// Intent → Effect → Result → Reducer → State
    ///
    /// 규칙:
    ///   View는 Accept(Intent)만 호출한다.
    ///   Model은 State만 발행한다. View를 직접 조작하지 않는다.
    ///   Reducer는 순수 함수다. 비동기 처리는 Effect 메서드에서만 한다.
    /// </summary>
    public sealed class InGameModel : IInitializable, IDisposable
    {
        private readonly ISocketSession _socketSession;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        private readonly ReactiveProperty<InGameState> _state
            = new ReactiveProperty<InGameState>(InGameState.Initial);

        private bool _isProcessing;

        public ReadOnlyReactiveProperty<InGameState> State =>
            _state.ToReadOnlyReactiveProperty();

        public InGameModel(ISocketSession socketSession)
        {
            _socketSession = socketSession;
        }

        public void Initialize() { }

        // ── View의 단일 진입점 ────────────────────────

        public void Accept(InGameIntent intent)
        {
            if (_isProcessing)
            {
                Debug.LogWarning($"[InGameModel] {intent.GetType().Name} 무시됨 — 처리 중");
                return;
            }

            if (intent is InGameIntent.ReturnToLobby)
                ReturnToLobbyAsync().Forget();
        }

        // ── Effect ───────────────────────────────────

        private async UniTaskVoid ReturnToLobbyAsync()
        {
            _isProcessing = true;
            Dispatch(InGameResult.Returning.Instance);
            try
            {
                // 1. TCP 방 퇴장 패킷 전송 (다른 플레이어에게 S_PlayerLeft 브로드캐스트)
                Debug.Log("[InGameModel] C_PlayerLeave 전송 중...");
                await _socketSession.LeaveRoomAsync(_cts.Token);

                // 2. TCP 소켓 연결 해제
                Debug.Log("[InGameModel] 소켓 연결 해제 중...");
                await _socketSession.DisconnectAsync(_cts.Token);
                Debug.Log("[InGameModel] 소켓 연결 해제 완료");

                // 3. Main 씬으로 복귀
                Debug.Log("[InGameModel] Main 씬으로 복귀");
                await SceneManager.LoadSceneAsync("Main");
            }
            catch (OperationCanceledException)
            {
                Debug.Log("[InGameModel] ReturnToLobby 취소됨");
                _isProcessing = false;
            }
            catch (Exception e)
            {
                Debug.LogError($"[InGameModel] ReturnToLobby 실패: {e}");
                Dispatch(new InGameResult.Failed(e.Message));
                _isProcessing = false;
            }
        }

        // ── Result → Reducer → State ──────────────────

        private void Dispatch(InGameResult result)
        {
            _state.Value = InGameReducer.Reduce(_state.Value, result);
        }

        public void Dispose()
        {
            _cts.Cancel();
            _cts.Dispose();
            _state.Dispose();
        }
    }
}
