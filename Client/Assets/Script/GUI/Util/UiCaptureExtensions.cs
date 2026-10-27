using Game.Presentation.DungeonLobby;

namespace Game.GUI
{
    /// <summary>
    /// OutGame 모달의 입력 점유를 닫힘 생명주기에 묶는 헬퍼. `inst.CaptureWhileOpen(model)` 로 즉시 점유하고
    /// AddressableInstance.Dispose() 시 자동 해제한다(닫는 쪽은 Dispose 만 하면 된다).
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
