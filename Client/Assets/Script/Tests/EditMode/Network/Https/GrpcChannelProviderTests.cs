using System;
using Game.Network.Https.Core;
using NUnit.Framework;

namespace Game.Tests.EditMode.Network
{
    [TestFixture]
    public class GrpcChannelProviderTests
    {
        // ─── Construction Validation ────────────────────────────────

        [Test]
        public void null_address_ArgumentException_발생()
        {
            Assert.Throws<ArgumentException>(() => new GrpcChannelProvider(null));
        }

        [Test]
        public void 빈_address_ArgumentException_발생()
        {
            Assert.Throws<ArgumentException>(() => new GrpcChannelProvider(""));
        }

        [Test]
        public void 공백_address_ArgumentException_발생()
        {
            Assert.Throws<ArgumentException>(() => new GrpcChannelProvider("   "));
        }

        // ─── Properties ─────────────────────────────────────────────

        [Test]
        public void 유효한_address로_생성시_Address_프로퍼티_일치()
        {
            const string address = "http://localhost:5132";
            using var provider = new GrpcChannelProvider(address);
            Assert.AreEqual(address, provider.Address);
        }

        [Test]
        public void Channel_null이_아님()
        {
            using var provider = new GrpcChannelProvider("http://localhost:5132");
            Assert.IsNotNull(provider.Channel);
        }

        [Test]
        public void CallInvoker_null이_아님()
        {
            using var provider = new GrpcChannelProvider("http://localhost:5132");
            Assert.IsNotNull(provider.CallInvoker);
        }

        [Test]
        public void CallInvoker_호출마다_새_인스턴스_반환()
        {
            using var provider = new GrpcChannelProvider("http://localhost:5132");
            var invoker1 = provider.CallInvoker;
            var invoker2 = provider.CallInvoker;
            // GrpcChannel.CreateCallInvoker() 는 매번 새 인스턴스를 반환한다
            Assert.AreNotSame(invoker1, invoker2);
        }

        // ─── Dispose ────────────────────────────────────────────────

        [Test]
        public void Dispose_예외없이_종료()
        {
            var provider = new GrpcChannelProvider("http://localhost:5132");
            Assert.DoesNotThrow(() => provider.Dispose());
        }

        [Test]
        public void Dispose_두번_예외없이_종료()
        {
            var provider = new GrpcChannelProvider("http://localhost:5132");
            provider.Dispose();
            Assert.DoesNotThrow(() => provider.Dispose());
        }

        // ─── Multiple Instances ──────────────────────────────────────

        [Test]
        public void 서로_다른_address로_생성한_두_인스턴스는_다른_객체()
        {
            using var p1 = new GrpcChannelProvider("http://localhost:5132");
            using var p2 = new GrpcChannelProvider("http://localhost:5133");
            Assert.AreNotSame(p1, p2);
            Assert.AreNotEqual(p1.Address, p2.Address);
        }
    }
}
