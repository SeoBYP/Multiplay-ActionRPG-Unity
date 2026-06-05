using Game.Presentation.DungeonLobby;

namespace Game.GUI
{
    /// <summary>
    /// OutGame 모달의 입력 점유를 닫힘 생명주기에 묶는 헬퍼.
    ///
    /// 각 호출처가 BeginUiCapture/EndUiCapture를 짝지어 부르던 중복을 제거한다.
    /// 모달을 열고 `inst.CaptureWhileOpen(model)` 한 줄만 호출하면:
    ///   - 즉시 게임플레이 입력 점유 시작(BeginUiCapture),
    ///   - AddressableInstance.Dispose() 시 자동으로 해제(EndUiCapture).
    /// → 닫는 쪽은 그냥 Dispose()만 하면 된다(EndUiCapture 직접 호출 불필요).
    ///
    /// 캡처는 반드시 Presentation Model(LobbyModel) 경유 — GUI는 System(IInputContext) 직접 참조 금지.
    /// </summary>
    public static class UiCaptureExtensions
    {
        public static AddressableInstance CaptureWhileOpen(this AddressableInstance inst, LobbyModel model)
        {
            model.BeginUiCapture();
            inst.SetOnDisposed(model.EndUiCapture);
            return inst;
        }
    }
}
