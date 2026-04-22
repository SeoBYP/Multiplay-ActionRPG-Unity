using System;

namespace Game.Network.Https.Core
{
    /// <summary>
    /// Rx 스타일 콜백을 간단한 Action 조합으로 감싸는 범용 Observer 구현체.
    /// </summary>
    public sealed class AnonymousObserver<T> : IObserver<T>
    {
        private readonly Action<T> _onNext;
        private readonly Action<Exception> _onError;
        private readonly Action _onCompleted;

        /// <summary>
        /// 필수 onNext와 선택적 onError/onCompleted 콜백으로 Observer를 구성한다.
        /// </summary>
        public AnonymousObserver(Action<T> onNext, Action<Exception> onError = null, Action onCompleted = null)
        {
            _onNext = onNext ?? throw new ArgumentNullException(nameof(onNext));
            _onError = onError;
            _onCompleted = onCompleted;
        }

        /// <summary>
        /// 새 데이터 수신 시 등록된 콜백을 실행한다.
        /// </summary>
        public void OnNext(T value) => _onNext(value);

        /// <summary>
        /// 스트림 에러를 외부 콜백에 전달한다.
        /// </summary>
        public void OnError(Exception error) => _onError?.Invoke(error);

        /// <summary>
        /// 스트림 완료 이벤트를 외부 콜백에 전달한다.
        /// </summary>
        public void OnCompleted() => _onCompleted?.Invoke();
    }
}
