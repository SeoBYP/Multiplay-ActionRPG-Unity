using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Grpc.Core;

namespace Game.Tests.EditMode.Network.Fakes
{
    /// <summary>
    /// gRPC CallInvoker를 가짜로 구현해 실제 서버 없이 서비스 래퍼를 테스트한다.
    /// 메서드 이름(proto rpc 이름)별로 응답 또는 에러를 미리 등록해두고,
    /// 실제 호출이 오면 등록된 값을 반환한다.
    /// </summary>
    public sealed class FakeCallInvoker : CallInvoker
    {
        private readonly Dictionary<string, object> _unaryResponses = new();
        private readonly Dictionary<string, IEnumerable<object>> _streamingResponses = new();
        private readonly Dictionary<string, RpcException> _errors = new();

        // ─── 응답 등록 API ──────────────────────────────────────────

        public void SetResponse<T>(string methodName, T response) where T : class
            => _unaryResponses[methodName] = response;

        public void SetStreamResponses<T>(string methodName, IEnumerable<T> responses) where T : class
        {
            var boxed = new List<object>();
            foreach (var r in responses) boxed.Add(r);
            _streamingResponses[methodName] = boxed;
        }

        public void SetError(string methodName, StatusCode code, string message)
            => _errors[methodName] = new RpcException(new Status(code, message));

        // ─── Unary ──────────────────────────────────────────────────

        public override TResponse BlockingUnaryCall<TRequest, TResponse>(
            Method<TRequest, TResponse> method, string host, CallOptions options, TRequest request)
            => throw new NotSupportedException("BlockingUnaryCall은 테스트에서 사용하지 않는다.");

        public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
            Method<TRequest, TResponse> method, string host, CallOptions options, TRequest request)
        {
            if (_errors.TryGetValue(method.Name, out var error))
                return MakeFailedUnaryCall<TResponse>(error);

            if (_unaryResponses.TryGetValue(method.Name, out var raw) && raw is TResponse response)
                return MakeSuccessUnaryCall(response);

            throw new InvalidOperationException(
                $"[FakeCallInvoker] '{method.Name}' 에 대한 응답이 등록되지 않았다. SetResponse() 또는 SetError()를 먼저 호출하라.");
        }

        // ─── Server Streaming ───────────────────────────────────────

        public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(
            Method<TRequest, TResponse> method, string host, CallOptions options, TRequest request)
        {
            if (_errors.TryGetValue(method.Name, out var error))
                return MakeFailedServerStreamingCall<TResponse>(error);

            if (_streamingResponses.TryGetValue(method.Name, out var rawList))
            {
                var typed = new List<TResponse>();
                foreach (var item in rawList)
                    typed.Add((TResponse)item);

                var reader = new FakeAsyncStreamReader<TResponse>(typed);
                return MakeSuccessServerStreamingCall(reader);
            }

            throw new InvalidOperationException(
                $"[FakeCallInvoker] '{method.Name}' 에 대한 스트리밍 응답이 등록되지 않았다. SetStreamResponses()를 먼저 호출하라.");
        }

        // ─── Client Streaming / BiDi ─────────────────────────────────

        public override AsyncClientStreamingCall<TRequest, TResponse> AsyncClientStreamingCall<TRequest, TResponse>(
            Method<TRequest, TResponse> method, string host, CallOptions options)
            => throw new NotSupportedException("Client streaming은 FakeCallInvoker에서 지원하지 않는다.");

        public override AsyncDuplexStreamingCall<TRequest, TResponse> AsyncDuplexStreamingCall<TRequest, TResponse>(
            Method<TRequest, TResponse> method, string host, CallOptions options)
            => throw new NotSupportedException("BiDi streaming은 FakeCallInvoker에서 지원하지 않는다.");

        // ─── 헬퍼 ───────────────────────────────────────────────────

        private static AsyncUnaryCall<T> MakeSuccessUnaryCall<T>(T response)
            => new(
                Task.FromResult(response),
                Task.FromResult(new Metadata()),
                () => Status.DefaultSuccess,
                () => new Metadata(),
                () => { });

        private static AsyncUnaryCall<T> MakeFailedUnaryCall<T>(RpcException error)
            => new(
                Task.FromException<T>(error),
                Task.FromResult(new Metadata()),
                () => new Status(error.StatusCode, error.Status.Detail),
                () => new Metadata(),
                () => { });

        private static AsyncServerStreamingCall<T> MakeSuccessServerStreamingCall<T>(
            IAsyncStreamReader<T> reader)
            => new(
                reader,
                Task.FromResult(new Metadata()),
                () => Status.DefaultSuccess,
                () => new Metadata(),
                () => { });

        private static AsyncServerStreamingCall<T> MakeFailedServerStreamingCall<T>(RpcException error)
            => new(
                new FakeAsyncStreamReader<T>(Array.Empty<T>(), error),
                Task.FromResult(new Metadata()),
                () => new Status(error.StatusCode, error.Status.Detail),
                () => new Metadata(),
                () => { });
    }
}
