using System;
using Game.Gameplay.Input;
using VContainer;
using VContainer.Unity;

namespace Game.Installers
{
    /// <summary>
    /// 앱 전역 입력 초기화 — 루트(ProjectLifetimeScope)에서 1회 실행.
    ///
    /// PlayerInputActions는 루트 Singleton(전 씬 공유)이므로 입력 맵 활성화도 루트에서 한 번만 한다.
    /// 씬별 초기화(구 Main/DungeonSceneInitializer)를 두면 씬마다 Enable/Dispose가 갈려
    /// "한 씬에서만 입력이 들어오는" 버그가 난다 → 전역 단일 책임으로 통합.
    ///
    /// UI 점유 중 Player 맵 토글은 IInputContext(역시 루트 Singleton)가 담당한다.
    /// PlayerInputActions(IDisposable)의 해제는 루트 스코프 teardown 시 VContainer가 1회 수행하므로
    /// 여기서 수동 Dispose하지 않는다(이중 해제 방지).
    ///
    /// 다만 <b>켠 것은 여기서 끈다</b>. 맵이 켜진 채로 asset 이 파괴되면 생성 래퍼의 종료자가
    /// "…Player.Disable() has not been called" assert 를 띄우고, 그 로그가 그때 실행 중이던
    /// <b>무관한 PlayMode 테스트에 달라붙어 실패</b>시킨다(실제로 SessionKeepAliveTests 가 그렇게 깨졌다).
    /// </summary>
    public class GlobalInputInitializer : IInitializable, IDisposable
    {
        [Inject] private PlayerInputActions _playerInputActions;

        public void Initialize()
        {
            _playerInputActions.Enable();
            _playerInputActions.Player.Enable();
            _playerInputActions.UI.Enable();
            UnityEngine.Debug.Log("[GlobalInputInitializer] 전역 입력 맵 활성화 완료");
        }

        public void Dispose()
        {
            if (_playerInputActions == null) return;

            try
            {
                _playerInputActions.Disable();
            }
            catch (Exception e)
            {
                // 루트 teardown 의 해제 순서는 보장되지 않는다. asset 이 이미 파괴됐다면 끌 것도 없다.
                // (여기서 예외를 흘리면 스코프 teardown 전체가 깨진다.)
                UnityEngine.Debug.LogWarning($"[GlobalInputInitializer] 입력 맵 해제 생략: {e.GetType().Name}");
            }
        }
    }
}
