using System.Collections.Generic;
using Game.Input;

namespace Game.Tests.EditMode.Input.Fakes
{
    /// <summary>
    /// IInputRouter 의 테스트용 Fake.
    /// 실제 PlayerInputActions / InputSystem 없이 Register / Unregister 동작만 검증한다.
    /// </summary>
    internal class FakeInputRouter : IInputRouter
    {
        public readonly List<IInputHandler> Registered = new List<IInputHandler>();

        public void Register(IInputHandler handler)   => Registered.Add(handler);
        public void Unregister(IInputHandler handler) => Registered.Remove(handler);
    }
}
