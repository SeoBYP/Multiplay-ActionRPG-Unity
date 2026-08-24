using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer.Unity;

namespace Game.System.Auth
{
    /// <summary>
    /// 액세스 토큰이 만료되기 전에 주기적으로 갱신해 세션을 살려 둔다.
    /// </summary>
    /// <remarks>
    /// 두 가지를 동시에 해결한다.
    /// ① <b>버그</b> — 갱신은 콜드스타트(<see cref="IAuthService.TryAutoLoginAsync"/>)에서만 일어났다.
    ///    액세스 토큰 수명(서버 설정 60분)을 넘겨 플레이하면 그 뒤 모든 gRPC 가 Unauthenticated 로 죽었다.
    /// ② <b>생존 신호</b> — 서버는 인증된 RPC 를 받을 때만 세션을 살아 있다고 표시한다.
    ///    로비에 가만히 있는 클라이언트는 아무 RPC 도 보내지 않아 "조용한" 것으로 보이고,
    ///    유령 방 리퍼가 그 방을 정리해 버릴 수 있었다.
    ///
    /// 전용 하트비트 RPC 를 새로 만들지 않은 이유 = 갱신 자체가 이미 인증된 왕복이라
    /// 공개 계약(proto)을 건드리지 않고 같은 목적을 달성한다.
    /// </remarks>
    public sealed class SessionKeepAlive : IAsyncStartable, IDisposable
    {
        private readonly AuthSession _session;
        private readonly IAuthService _authService;
        private readonly double _refreshAtRatio;
        private readonly TimeSpan _minDelay;
        private readonly CancellationTokenSource _cts = new();

        /// <param name="refreshAtRatio">남은 수명의 이 비율만큼 기다린 뒤 갱신한다(0.6 = 40% 여유를 남김).</param>
        /// <param name="minDelay">토큰이 이미 만료에 가까울 때 갱신 폭주를 막는 하한.</param>
        public SessionKeepAlive(
            AuthSession session,
            IAuthService authService,
            double refreshAtRatio = 0.6,
            TimeSpan? minDelay = null)
        {
            _session = session;
            _authService = authService;
            _refreshAtRatio = refreshAtRatio;
            _minDelay = minDelay ?? TimeSpan.FromSeconds(30);
        }

        public async UniTask StartAsync(CancellationToken cancellation)
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellation, _cts.Token);
            var ct = linked.Token;

            try
            {
                await _session.AuthenticatedAsync();

                while (!ct.IsCancellationRequested)
                {
                    // 실시간 기준으로 잰다 — timeScale 을 0 으로 두는 화면(일시정지·로딩)에서도 세션은 만료된다.
                    await UniTask.Delay(NextDelay(), ignoreTimeScale: true, cancellationToken: ct);

                    var result = await _authService.RefreshTokenAsync(ct);
                    if (result != AuthResult.Success)
                    {
                        // 갱신이 실패하면 상위 흐름(로그인 화면 복귀)이 처리한다. 여기서 루프를 붙들지 않는다.
                        Debug.LogWarning($"[SessionKeepAlive] 토큰 갱신 실패({result}) — keep-alive 를 중단한다");
                        return;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // 정상 종료.
            }
        }

        private TimeSpan NextDelay()
        {
            var remaining = DateTimeOffset.FromUnixTimeSeconds(_session.ExpiresAt) - DateTimeOffset.UtcNow;
            var delay = TimeSpan.FromTicks((long)(remaining.Ticks * _refreshAtRatio));
            return delay < _minDelay ? _minDelay : delay;
        }

        public void Dispose()
        {
            _cts.Cancel();
            _cts.Dispose();
        }
    }
}
