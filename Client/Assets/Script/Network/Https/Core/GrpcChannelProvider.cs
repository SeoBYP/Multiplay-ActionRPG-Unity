using System;
using System.Linq;
using System.Net.Http;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Grpc.Net.Client;
using Cysharp.Net.Http;

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

            _channel = GrpcChannel.ForAddress(address, new GrpcChannelOptions
            {
                HttpHandler = CreateHttpHandler(address)
            });
        }

        // 테스트 전용: FakeCallInvoker 주입용
        protected GrpcChannelProvider(CallInvoker overrideInvoker)
        {
            Address = "fake://test";
            _overrideInvoker = overrideInvoker ?? throw new ArgumentNullException(nameof(overrideInvoker));
        }

        public string Address { get; }

        public GrpcChannel Channel => _channel;

        public Func<string> AccessTokenProvider { get; set; }

        public virtual CallInvoker CallInvoker
            => (_overrideInvoker ?? _channel.CreateCallInvoker())
                .Intercept(new AuthorizationInterceptor(this));

        public virtual void Dispose() => _channel?.Dispose();

        private static HttpMessageHandler CreateHttpHandler(string address)
        {
            var handler = new YetAnotherHttpHandler();

            // Plaintext gRPC requires h2c. YetAnotherHttpHandler uses HTTP/1.1
            // unless HTTP/2-only is enabled for http:// endpoints.
            if (Uri.TryCreate(address, UriKind.Absolute, out var uri) &&
                uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
            {
                handler.Http2Only = true;
            }

            return handler;
        }

        private sealed class AuthorizationInterceptor : Interceptor
        {
            private readonly GrpcChannelProvider _provider;

            public AuthorizationInterceptor(GrpcChannelProvider provider)
            {
                _provider = provider;
            }

            public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
                TRequest request,
                ClientInterceptorContext<TRequest, TResponse> context,
                AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
            {
                return continuation(request, CreateContext(context));
            }

            public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(
                TRequest request,
                ClientInterceptorContext<TRequest, TResponse> context,
                AsyncServerStreamingCallContinuation<TRequest, TResponse> continuation)
            {
                return continuation(request, CreateContext(context));
            }

            public override AsyncClientStreamingCall<TRequest, TResponse> AsyncClientStreamingCall<TRequest, TResponse>(
                ClientInterceptorContext<TRequest, TResponse> context,
                AsyncClientStreamingCallContinuation<TRequest, TResponse> continuation)
            {
                return continuation(CreateContext(context));
            }

            public override AsyncDuplexStreamingCall<TRequest, TResponse> AsyncDuplexStreamingCall<TRequest, TResponse>(
                ClientInterceptorContext<TRequest, TResponse> context,
                AsyncDuplexStreamingCallContinuation<TRequest, TResponse> continuation)
            {
                return continuation(CreateContext(context));
            }

            public override TResponse BlockingUnaryCall<TRequest, TResponse>(
                TRequest request,
                ClientInterceptorContext<TRequest, TResponse> context,
                BlockingUnaryCallContinuation<TRequest, TResponse> continuation)
            {
                return continuation(request, CreateContext(context));
            }

            private ClientInterceptorContext<TRequest, TResponse> CreateContext<TRequest, TResponse>(
                ClientInterceptorContext<TRequest, TResponse> context)
                where TRequest : class
                where TResponse : class
            {
                var headers = new Metadata();
                if (context.Options.Headers is not null)
                {
                    foreach (var entry in context.Options.Headers)
                    {
                        headers.Add(entry.Key, entry.Value);
                    }
                }

                var accessToken = _provider.AccessTokenProvider?.Invoke();
                if (!string.IsNullOrWhiteSpace(accessToken) &&
                    !headers.Any(entry => string.Equals(entry.Key, "authorization", StringComparison.OrdinalIgnoreCase)))
                {
                    headers.Add("authorization", $"Bearer {accessToken}");
                }

                var options = new CallOptions(
                    headers,
                    context.Options.Deadline,
                    context.Options.CancellationToken,
                    context.Options.WriteOptions,
                    context.Options.PropagationToken,
                    context.Options.Credentials);

                return new ClientInterceptorContext<TRequest, TResponse>(context.Method, context.Host, options);
            }
        }
    }
}
