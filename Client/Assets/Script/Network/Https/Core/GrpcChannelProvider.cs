using System;
using System.Linq;
using System.Net.Http;
using Cysharp.Net.Http;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Grpc.Net.Client;

namespace Game.Network.Https.Core
{
    /// <summary>
    /// gRPC 채널 생성과 공통 Authorization 헤더 주입을 담당하는 공급자.
    /// </summary>
    public class GrpcChannelProvider : IDisposable
    {
        private readonly GrpcChannel _channel;
        private readonly CallInvoker _overrideInvoker;

        /// <summary>
        /// 실제 서버 주소를 사용해 gRPC 채널을 생성한다.
        /// </summary>
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

        /// <summary>
        /// 테스트 전용 생성자. 외부 CallInvoker를 주입해 네트워크 없는 테스트를 가능하게 한다.
        /// </summary>
        protected GrpcChannelProvider(CallInvoker overrideInvoker)
        {
            Address = "fake://test";
            _overrideInvoker = overrideInvoker ?? throw new ArgumentNullException(nameof(overrideInvoker));
        }

        public string Address { get; }
        public GrpcChannel Channel => _channel;

        /// <summary>
        /// 현재 access token을 반환하는 콜백. 각 RPC 직전에 호출된다.
        /// </summary>
        public Func<string> AccessTokenProvider { get; set; }

        /// <summary>
        /// 매 호출마다 현재 access token을 헤더에 실어 보내는 CallInvoker를 반환한다.
        /// </summary>
        public virtual CallInvoker CallInvoker
            => (_overrideInvoker ?? _channel.CreateCallInvoker())
                .Intercept(new AuthorizationInterceptor(this));

        /// <summary>
        /// 내부 gRPC 채널을 정리한다.
        /// </summary>
        public virtual void Dispose() => _channel?.Dispose();

        /// <summary>
        /// 주소 스킴에 맞는 HTTP 핸들러를 구성한다.
        /// </summary>
        private static HttpMessageHandler CreateHttpHandler(string address)
        {
            var handler = new YetAnotherHttpHandler();

            // Plaintext gRPC는 h2c가 필요하므로 http:// 주소일 때 HTTP/2 전용 모드로 강제한다.
            if (Uri.TryCreate(address, UriKind.Absolute, out var uri) &&
                uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
            {
                handler.Http2Only = true;
            }

            return handler;
        }

        /// <summary>
        /// RPC 호출 직전에 Authorization 헤더를 추가하는 인터셉터.
        /// </summary>
        private sealed class AuthorizationInterceptor : Interceptor
        {
            private readonly GrpcChannelProvider _provider;

            /// <summary>
            /// 현재 채널 공급자를 기준으로 헤더 주입 인터셉터를 생성한다.
            /// </summary>
            public AuthorizationInterceptor(GrpcChannelProvider provider)
            {
                _provider = provider;
            }

            /// <summary>
            /// Unary 호출에 인증 헤더가 포함된 컨텍스트를 적용한다.
            /// </summary>
            public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
                TRequest request,
                ClientInterceptorContext<TRequest, TResponse> context,
                AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
            {
                return continuation(request, CreateContext(context));
            }

            /// <summary>
            /// Server streaming 호출에 인증 헤더가 포함된 컨텍스트를 적용한다.
            /// </summary>
            public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(
                TRequest request,
                ClientInterceptorContext<TRequest, TResponse> context,
                AsyncServerStreamingCallContinuation<TRequest, TResponse> continuation)
            {
                return continuation(request, CreateContext(context));
            }

            /// <summary>
            /// Client streaming 호출에 인증 헤더가 포함된 컨텍스트를 적용한다.
            /// </summary>
            public override AsyncClientStreamingCall<TRequest, TResponse> AsyncClientStreamingCall<TRequest, TResponse>(
                ClientInterceptorContext<TRequest, TResponse> context,
                AsyncClientStreamingCallContinuation<TRequest, TResponse> continuation)
            {
                return continuation(CreateContext(context));
            }

            /// <summary>
            /// Duplex streaming 호출에 인증 헤더가 포함된 컨텍스트를 적용한다.
            /// </summary>
            public override AsyncDuplexStreamingCall<TRequest, TResponse> AsyncDuplexStreamingCall<TRequest, TResponse>(
                ClientInterceptorContext<TRequest, TResponse> context,
                AsyncDuplexStreamingCallContinuation<TRequest, TResponse> continuation)
            {
                return continuation(CreateContext(context));
            }

            /// <summary>
            /// 동기 Unary 호출에도 동일한 인증 헤더 주입 규칙을 적용한다.
            /// </summary>
            public override TResponse BlockingUnaryCall<TRequest, TResponse>(
                TRequest request,
                ClientInterceptorContext<TRequest, TResponse> context,
                BlockingUnaryCallContinuation<TRequest, TResponse> continuation)
            {
                return continuation(request, CreateContext(context));
            }

            /// <summary>
            /// 기존 호출 컨텍스트에 Authorization 헤더를 병합한 새 컨텍스트를 만든다.
            /// </summary>
            private ClientInterceptorContext<TRequest, TResponse> CreateContext<TRequest, TResponse>(
                ClientInterceptorContext<TRequest, TResponse> context)
                where TRequest : class
                where TResponse : class
            {
                var headers = new Metadata();

                // 기존 호출자가 넣은 헤더는 유지한다.
                if (context.Options.Headers is not null)
                {
                    foreach (var entry in context.Options.Headers)
                    {
                        headers.Add(entry.Key, entry.Value);
                    }
                }

                var accessToken = _provider.AccessTokenProvider?.Invoke();
                // access token이 있고 이미 authorization 헤더가 없을 때만 Bearer 토큰을 추가한다.
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
