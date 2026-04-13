using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;

namespace Game.Tests.EditMode.Network.Fakes
{
    /// <summary>
    /// ServerStreaming 테스트용 IAsyncStreamReader 구현체.
    /// 생성 시 전달한 메시지 목록을 순서대로 반환하고,
    /// error가 지정된 경우 MoveNext 호출 즉시 해당 예외를 던진다.
    /// </summary>
    public sealed class FakeAsyncStreamReader<T> : IAsyncStreamReader<T>
    {
        private readonly IEnumerator<T> _enumerator;
        private readonly RpcException _error;

        public FakeAsyncStreamReader(IEnumerable<T> messages, RpcException error = null)
        {
            _enumerator = messages.GetEnumerator();
            _error = error;
        }

        public T Current => _enumerator.Current;

        public Task<bool> MoveNext(CancellationToken cancellationToken)
        {
            if (_error != null)
                return Task.FromException<bool>(_error);

            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_enumerator.MoveNext());
        }
    }
}
