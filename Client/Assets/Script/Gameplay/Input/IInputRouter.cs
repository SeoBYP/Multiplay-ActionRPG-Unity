namespace Game.Gameplay.Input
{
    /// <summary>
    /// InputRouter 의 최소 계약.
    /// InteractionSystem 등 핸들러가 자신을 등록/해제할 때만 이 인터페이스에 의존하도록 분리.
    /// 테스트에서 FakeInputRouter 로 교체 가능하다.
    /// </summary>
    public interface IInputRouter
    {
        void Register(IInputHandler handler);
        void Unregister(IInputHandler handler);
    }
}
