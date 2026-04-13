using Game.Network.Https.Core;

namespace Game.Tests.EditMode.Network.Fakes
{
    /// <summary>
    /// 테스트 전용 GrpcChannelProvider.
    /// 실제 GrpcChannel 대신 FakeCallInvoker를 주입해 네트워크 없이 서비스를 테스트한다.
    /// </summary>
    public sealed class FakeGrpcChannelProvider : GrpcChannelProvider
    {
        public FakeGrpcChannelProvider(FakeCallInvoker invoker) : base(invoker) { }

        public override void Dispose() { }
    }
}
