using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Game.Network.Socket;
using Game.Network.Socket.Packets;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode.Network.Socket
{
    /// <summary>
    /// SocketSession keep-alive 핑 단위 테스트 — Docker 불필요(Fake 커넥터). 60초 대기 불필요(짧은 인터벌 주입).
    /// 서버 유휴 타임아웃(방 60s) 방지의 클라 측 보증: 무이동 상태에서도 주기적으로 C_Ping 을 보낸다.
    /// </summary>
    [TestFixture]
    public class SocketSessionHeartbeatTests
    {
        /// <summary>송신된 C_Ping 수를 세고, 수신 루프는 취소까지 열어두는(연결 유지) Fake 커넥터.</summary>
        private sealed class FakeConnector : ISocketConnector
        {
            public int PingCount;
            public bool IsConnected { get; private set; }

            public UniTask ConnectAsync(string host, int port, CancellationToken ct)
            {
                IsConnected = true;
                return UniTask.CompletedTask;
            }

            public UniTask SendAsync(Packet packet, CancellationToken ct)
            {
                if (packet is C_Ping) PingCount++;
                return UniTask.CompletedTask;
            }

            // 실제 연결처럼 취소될 때까지 열어둔다(즉시 반환하면 세션이 Disconnected 로 끝나 핑이 멈춤).
            public UniTask StartReceiveLoopAsync(Func<Packet, UniTask> onPacket, CancellationToken ct)
                => UniTask.Never(ct);

            public UniTask DisconnectAsync(CancellationToken ct)
            {
                IsConnected = false;
                return UniTask.CompletedTask;
            }

            public ValueTask DisposeAsync() => default;
        }

        private sealed class NoopDispatcher : ISocketPacketDispatcher
        {
            public UniTask DispatchAsync(Packet packet, CancellationToken ct = default) => UniTask.CompletedTask;
        }

        private static SocketConnectionInfo Info() => new("127.0.0.1", 7777, 1, 1);

        [UnityTest]
        public IEnumerator 연결되면_주기적으로_C_Ping을_보낸다() => UniTask.ToCoroutine(async () =>
        {
            var connector = new FakeConnector();
            var session = new SocketSession(connector, new NoopDispatcher())
            {
                HeartbeatInterval = TimeSpan.FromMilliseconds(150)
            };

            await session.ConnectAsync(Info(), CancellationToken.None); // State=Connected → 핑 루프 시작
            await UniTask.Delay(TimeSpan.FromMilliseconds(550), ignoreTimeScale: true); // ~3 인터벌

            Assert.GreaterOrEqual(connector.PingCount, 2, "무이동 상태에서도 주기적으로 C_Ping 송신되어야 함");

            await session.DisconnectAsync(CancellationToken.None);
        });

        [UnityTest]
        public IEnumerator 끊기면_더이상_C_Ping을_보내지_않는다() => UniTask.ToCoroutine(async () =>
        {
            var connector = new FakeConnector();
            var session = new SocketSession(connector, new NoopDispatcher())
            {
                HeartbeatInterval = TimeSpan.FromMilliseconds(150)
            };

            await session.ConnectAsync(Info(), CancellationToken.None);
            await UniTask.Delay(TimeSpan.FromMilliseconds(350), ignoreTimeScale: true);

            await session.DisconnectAsync(CancellationToken.None);
            int afterDisconnect = connector.PingCount;

            await UniTask.Delay(TimeSpan.FromMilliseconds(450), ignoreTimeScale: true); // 3 인터벌 더 — 멈췄어야

            Assert.AreEqual(afterDisconnect, connector.PingCount, "끊김(세션 토큰 취소) 후엔 핑 루프가 멈춰야 함");
        });
    }
}
