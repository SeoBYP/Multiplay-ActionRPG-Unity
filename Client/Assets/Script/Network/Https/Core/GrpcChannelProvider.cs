using System;
using Grpc.Core;
using Grpc.Net.Client;

namespace Game.Network.Https.Core
{
    public class GrpcChannelProvider : IDisposable
    {
        private readonly GrpcChannel _channel;
        private readonly CallInvoker _overrideInvoker;

        public GrpcChannelProvider(string address)
        {
            if (string.IsNullOrWhiteSpace(address))
                throw new ArgumentException("gRPC address is required.", nameof(address));

            Address = address;
            _channel = GrpcChannel.ForAddress(address);
        }

        // 테스트 전용: FakeCallInvoker 주입용
        protected GrpcChannelProvider(CallInvoker overrideInvoker)
        {
            Address = "fake://test";
            _overrideInvoker = overrideInvoker ?? throw new ArgumentNullException(nameof(overrideInvoker));
        }

        public string Address { get; }

        public GrpcChannel Channel => _channel;

        public virtual CallInvoker CallInvoker => _overrideInvoker ?? _channel.CreateCallInvoker();

        public virtual void Dispose() => _channel?.Dispose();
    }
}
