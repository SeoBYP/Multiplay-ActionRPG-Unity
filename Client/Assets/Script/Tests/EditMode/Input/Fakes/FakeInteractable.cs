using Game.Gameplay.Input;
using UnityEngine;

namespace Game.Tests.EditMode.Input.Fakes
{
    /// <summary>
    /// IInteractable 의 테스트용 Fake.
    /// CanInteract / InteractionPriority 를 생성자에서 설정하고
    /// Interact() 호출 여부를 InteractCalled 로 추적한다.
    /// </summary>
    internal class FakeInteractable : IInteractable
    {
        public string InteractionHint      { get; }
        public bool   CanInteract          { get; }
        public int    InteractionPriority  { get; }
        public Transform Transform         => null; // 위치 기반 Score 테스트가 필요하면 별도 구성

        public bool InteractCalled { get; private set; }

        public FakeInteractable(bool canInteract, int priority = 0, string hint = "Test")
        {
            CanInteract         = canInteract;
            InteractionPriority = priority;
            InteractionHint     = hint;
        }

        public void Interact() => InteractCalled = true;
    }
}
