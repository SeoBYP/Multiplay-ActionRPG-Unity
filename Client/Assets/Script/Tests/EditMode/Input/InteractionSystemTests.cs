using Game.Gameplay.Input;
using Game.Tests.EditMode.Input.Fakes;
using NUnit.Framework;

namespace Game.Tests.EditMode.Input
{
    /// <summary>
    /// InteractionSystem 단위 테스트.
    ///
    /// IInputRouter, IInteractable 을 Fake 로 교체해서
    /// 실제 Input System / MonoBehaviour 없이 로직만 검증한다.
    /// </summary>
    [TestFixture]
    public class InteractionSystemTests
    {
        private FakeInputRouter   _router;
        private InteractionSystem _system;

        [SetUp]
        public void SetUp()
        {
            _router = new FakeInputRouter();
            _system = new InteractionSystem(_router);
            _system.Initialize();
        }

        [TearDown]
        public void TearDown()
        {
            _system.Dispose();
        }

        // ── Initialize / Dispose ─────────────────────────────────────

        [Test]
        public void 초기화하면_라우터에_등록된다()
        {
            Assert.IsTrue(_router.Registered.Contains(_system));
        }

        [Test]
        public void Dispose하면_라우터에서_해제된다()
        {
            _system.Dispose();
            Assert.IsFalse(_router.Registered.Contains(_system));
        }

        // ── TryHandle — 기본 동작 ────────────────────────────────────

        [Test]
        public void 현재_대상이_없으면_Interact_처리시_false_반환한다()
        {
            var result = _system.TryHandle(GameInputAction.Interact);
            Assert.IsFalse(result);
        }

        [Test]
        public void 유효한_대상이_있어도_Interact_외_액션은_false_반환한다()
        {
            _system.SetCurrent(new FakeInteractable(canInteract: true));

            Assert.IsFalse(_system.TryHandle(GameInputAction.Attack));
            Assert.IsFalse(_system.TryHandle(GameInputAction.ToggleLobby));
            Assert.IsFalse(_system.TryHandle(GameInputAction.Dodge));
        }

        [Test]
        public void CanInteract가_false이면_Interact_처리시_false_반환한다()
        {
            _system.SetCurrent(new FakeInteractable(canInteract: false));

            var result = _system.TryHandle(GameInputAction.Interact);

            Assert.IsFalse(result);
        }

        [Test]
        public void CanInteract가_true이면_Interact_처리시_true_반환하고_Interact를_호출한다()
        {
            var fake = new FakeInteractable(canInteract: true);
            _system.SetCurrent(fake);

            var result = _system.TryHandle(GameInputAction.Interact);

            Assert.IsTrue(result);
            Assert.IsTrue(fake.InteractCalled);
        }

        // ── SetCurrent ───────────────────────────────────────────────

        [Test]
        public void SetCurrent_호출하면_OnInteractableChanged_이벤트가_발생한다()
        {
            IInteractable received = null;
            _system.OnInteractableChanged += i => received = i;

            var fake = new FakeInteractable(canInteract: true);
            _system.SetCurrent(fake);

            Assert.AreEqual(fake, received);
        }

        [Test]
        public void SetCurrent_동일_오브젝트_재설정시_이벤트가_발생하지_않는다()
        {
            var fake = new FakeInteractable(canInteract: true);
            _system.SetCurrent(fake);

            var fired = false;
            _system.OnInteractableChanged += _ => fired = true;

            _system.SetCurrent(fake);

            Assert.IsFalse(fired);
        }

        [Test]
        public void SetCurrent에_null_전달하면_Current가_초기화되고_이벤트가_발생한다()
        {
            var fake = new FakeInteractable(canInteract: true);
            _system.SetCurrent(fake);

            IInteractable received = fake;
            _system.OnInteractableChanged += i => received = i;
            _system.SetCurrent(null);

            Assert.IsNull(received);
            Assert.IsNull(_system.Current);
        }

        // ── Priority ─────────────────────────────────────────────────

        [Test]
        public void 우선순위는_50이다()
        {
            Assert.AreEqual(50, _system.Priority);
        }
    }
}
