using System;
using Game.Network.Https.Core;
using NUnit.Framework;

namespace Game.Tests.EditMode.Network
{
    [TestFixture]
    public class AnonymousObserverTests
    {
        // ─── Construction ───────────────────────────────────────────

        [Test]
        public void null_onNext_ArgumentNullException_발생()
        {
            Assert.Throws<ArgumentNullException>(() => new AnonymousObserver<int>(null));
        }

        [Test]
        public void null_onError_허용됨()
        {
            Assert.DoesNotThrow(() => new AnonymousObserver<int>(_ => { }, onError: null));
        }

        [Test]
        public void null_onCompleted_허용됨()
        {
            Assert.DoesNotThrow(() => new AnonymousObserver<int>(_ => { }, onCompleted: null));
        }

        // ─── OnNext ─────────────────────────────────────────────────

        [Test]
        public void OnNext_등록된_액션이_호출됨()
        {
            var called = false;
            var observer = new AnonymousObserver<int>(_ => called = true);

            observer.OnNext(0);

            Assert.IsTrue(called);
        }

        [Test]
        public void OnNext_전달된_값이_그대로_전달됨()
        {
            var received = 0;
            var observer = new AnonymousObserver<int>(v => received = v);

            observer.OnNext(42);

            Assert.AreEqual(42, received);
        }

        [Test]
        public void OnNext_여러번_호출시_호출_횟수만큼_실행됨()
        {
            var count = 0;
            var observer = new AnonymousObserver<string>(_ => count++);

            observer.OnNext("a");
            observer.OnNext("b");
            observer.OnNext("c");

            Assert.AreEqual(3, count);
        }

        [Test]
        public void OnNext_참조_타입_전달됨()
        {
            object received = null;
            var expected = new object();
            var observer = new AnonymousObserver<object>(v => received = v);

            observer.OnNext(expected);

            Assert.AreSame(expected, received);
        }

        // ─── OnError ────────────────────────────────────────────────

        [Test]
        public void OnError_등록된_핸들러가_호출됨()
        {
            Exception received = null;
            var observer = new AnonymousObserver<int>(_ => { }, ex => received = ex);
            var error = new InvalidOperationException("fail");

            observer.OnError(error);

            Assert.AreEqual(error, received);
        }

        [Test]
        public void OnError_핸들러_미등록시_예외없이_종료()
        {
            var observer = new AnonymousObserver<int>(_ => { });

            Assert.DoesNotThrow(() => observer.OnError(new Exception("test")));
        }

        [Test]
        public void OnError_전달된_예외가_그대로_전달됨()
        {
            Exception received = null;
            var expected = new ArgumentOutOfRangeException("param");
            var observer = new AnonymousObserver<int>(_ => { }, ex => received = ex);

            observer.OnError(expected);

            Assert.AreSame(expected, received);
        }

        // ─── OnCompleted ────────────────────────────────────────────

        [Test]
        public void OnCompleted_등록된_액션이_호출됨()
        {
            var called = false;
            var observer = new AnonymousObserver<int>(_ => { }, onCompleted: () => called = true);

            observer.OnCompleted();

            Assert.IsTrue(called);
        }

        [Test]
        public void OnCompleted_핸들러_미등록시_예외없이_종료()
        {
            var observer = new AnonymousObserver<int>(_ => { });

            Assert.DoesNotThrow(() => observer.OnCompleted());
        }

        [Test]
        public void OnCompleted_여러번_호출시_호출_횟수만큼_실행됨()
        {
            var count = 0;
            var observer = new AnonymousObserver<int>(_ => { }, onCompleted: () => count++);

            observer.OnCompleted();
            observer.OnCompleted();

            Assert.AreEqual(2, count);
        }
    }
}
