using System;
using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.System.Auth;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode.Network
{
    /// <summary>
    /// 토큰 keep-alive 단위 테스트 — Docker 불필요(Fake 인증 서비스), 60분 대기 불필요(짧은 만료 주입).
    /// 두 가지를 고정한다: ① 만료 전에 스스로 갱신한다 ② 갱신이 실패하면 조용히 멈춘다.
    /// </summary>
    [TestFixture]
    public class SessionKeepAliveTests
    {
        /// <summary>픽스처 전용 저장소 — 전역 PlayerPrefs 를 건드리지 않는다.</summary>
        private sealed class InMemoryTokenStore : ITokenStore
        {
            private string _access, _refresh;
            private long _expiresAt;

            public void Save(string a, string r, long e) { _access = a; _refresh = r; _expiresAt = e; }

            public bool TryLoad(out string a, out string r, out long e)
            {
                a = _access; r = _refresh; e = _expiresAt;
                return !string.IsNullOrWhiteSpace(_access);
            }

            public void Clear() { _access = null; _refresh = null; _expiresAt = 0; }
        }

        private sealed class FakeAuthService : IAuthService
        {
            private readonly AuthSession _session;
            private readonly AuthResult _result;
            private readonly TimeSpan _lifetime;
            public int RefreshCount;
            public bool AlwaysRefreshedBeforeExpiry = true;

            public FakeAuthService(AuthSession session, AuthResult result = AuthResult.Success, TimeSpan? lifetime = null)
            {
                _session = session;
                _result = result;
                _lifetime = lifetime ?? TimeSpan.FromSeconds(2);
            }

            public bool IsAuthenticated => _session.IsAuthenticated;
            public UniTask AuthenticatedAsync() => _session.AuthenticatedAsync();

            public UniTask<AuthResult> RefreshTokenAsync(CancellationToken ct)
            {
                RefreshCount++;

                // 이 갱신이 "만료 전"이었는지 기록한다 — 세션이 한 번이라도 끊겼는지 판정하는 근거.
                if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > _session.ExpiresAt)
                    AlwaysRefreshedBeforeExpiry = false;

                if (_result == AuthResult.Success)
                {
                    // 서버가 새 토큰을 준 것처럼 만료를 뒤로 민다.
                    _session.Update("access", "refresh", DateTimeOffset.UtcNow.Add(_lifetime).ToUnixTimeSeconds());
                }
                return UniTask.FromResult(_result);
            }

            public UniTask<AuthResult> TryAutoLoginAsync(CancellationToken ct) => UniTask.FromResult(AuthResult.Success);
            public UniTask<AuthResult> LoginOrRegisterAsync(string email, string password, CancellationToken ct)
                => UniTask.FromResult(AuthResult.Success);
            public void Logout() { }
        }

        [UnityTest]
        public IEnumerator 만료_전에_스스로_토큰을_갱신한다() => UniTask.ToCoroutine(async () =>
        {
            var session = new AuthSession(new InMemoryTokenStore());
            // 남은 수명을 0 으로 둬서 첫 갱신이 minDelay 에 걸리게 한다(테스트를 1초 이상 끌지 않기 위함).
            session.Update("access", "refresh", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            var auth = new FakeAuthService(session);

            using var keepAlive = new SessionKeepAlive(
                session, auth, refreshAtRatio: 0.5, minDelay: TimeSpan.FromMilliseconds(50));
            using var cts = new CancellationTokenSource();

            keepAlive.StartAsync(cts.Token).Forget();
            await UniTask.Delay(TimeSpan.FromMilliseconds(600), ignoreTimeScale: true);
            cts.Cancel();

            Assert.Greater(auth.RefreshCount, 0, "만료 전에 갱신을 시도해야 한다");
        });

        [UnityTest]
        public IEnumerator 토큰_수명을_여러_번_넘겨도_세션이_계속_살아있다() => UniTask.ToCoroutine(async () =>
        {
            // "사람이 60분 넘게 플레이한다"를 시간 압축으로 관측한다.
            // 수명 200ms 짜리 토큰을 쓰면 1초 안에 여러 수명 주기가 지나간다.
            var session = new AuthSession(new InMemoryTokenStore());
            var auth = new FakeAuthService(session, lifetime: TimeSpan.FromMilliseconds(200));
            session.Update("access", "refresh", DateTimeOffset.UtcNow.ToUnixTimeSeconds());

            using var keepAlive = new SessionKeepAlive(
                session, auth, refreshAtRatio: 0.5, minDelay: TimeSpan.FromMilliseconds(20));
            using var cts = new CancellationTokenSource();

            keepAlive.StartAsync(cts.Token).Forget();
            await UniTask.Delay(TimeSpan.FromMilliseconds(800), ignoreTimeScale: true);
            cts.Cancel();

            Assert.GreaterOrEqual(auth.RefreshCount, 3, "수명이 여러 번 지나는 동안 계속 갱신해야 한다");
            // 매 갱신이 그 시점의 만료보다 앞섰다 = 세션이 끊긴 적이 없다.
            Assert.IsTrue(auth.AlwaysRefreshedBeforeExpiry, "갱신은 항상 만료 전에 일어나야 한다");
        });

        [UnityTest]
        public IEnumerator 갱신에_실패하면_루프를_멈춘다() => UniTask.ToCoroutine(async () =>
        {
            var session = new AuthSession(new InMemoryTokenStore());
            session.Update("access", "refresh", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            var auth = new FakeAuthService(session, AuthResult.NeedLogin);

            using var keepAlive = new SessionKeepAlive(
                session, auth, refreshAtRatio: 0.5, minDelay: TimeSpan.FromMilliseconds(50));
            using var cts = new CancellationTokenSource();

            keepAlive.StartAsync(cts.Token).Forget();
            await UniTask.Delay(TimeSpan.FromMilliseconds(600), ignoreTimeScale: true);
            cts.Cancel();

            Assert.AreEqual(1, auth.RefreshCount, "실패하면 재시도로 서버를 두드리지 않고 멈춰야 한다");
        });
    }
}
