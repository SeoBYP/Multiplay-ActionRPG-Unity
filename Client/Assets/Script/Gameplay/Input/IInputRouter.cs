namespace Game.Gameplay.Input
{
    /// <summary>
    /// InputRouter 의 최소 계약 — 핸들러가 자신을 등록/해제할 때만 의존한다. 테스트에서 FakeInputRouter 로 교체.
    /// </summary>
    public interface IInputRouter
    {
        void Register(IInputHandler handler);
        void Unregister(IInputHandler handler);
    }
}
