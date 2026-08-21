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
    /// 입장 거절 사유 전달 — Docker 불필요(Fake 커넥터).
    ///
    /// 서버는 거절할 때마다 사유를 <c>S_PlayerJoined.Message</c> 에 담아 보낸다("Room is full" 등 5종).
    /// 예전엔 세션이 <c>Success</c> 만 보고 상태만 Failed 로 바꾼 뒤 사유를 <b>버렸다</b> —
    /// 그래서 실제로 방이 가득 찼는데도 콘솔에는 `state=Failed` 만 30줄 남고 원인을 알 수 없었다(실측).
    /// </summary>
    [TestFixture]
    public class SocketSessionJoinFailureTests
    {
        /// <summary>서버 대신 지정한 S_PlayerJoined 를 한 번 흘려 주는 Fake.</summary>
        private sealed class ScriptedConnector : ISocketConnector
        {
            private readonly Packet _reply;
            public ScriptedConnector(Packet reply) => _reply = reply;

            public bool IsConnected { get; private set; }
            public UniTask ConnectAsync(string host, int port, CancellationToken ct)
            {
                IsConnected = true;
                return UniTask.CompletedTask;
            }

            public UniTask SendAsync(Packet packet, CancellationToken ct) => UniTask.CompletedTask;

            public async UniTask StartReceiveLoopAsync(Func<Packet, UniTask> onPacket, CancellationToken ct)
            {
                await UniTask.Delay(TimeSpan.FromMilliseconds(50), ignoreTimeScale: true, cancellationToken: ct);
                await onPacket(_reply);
                await UniTask.Never(ct);
            }

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
        public IEnumerator 입장_거절되면_서버가_보낸_사유를_보관한다() => UniTask.ToCoroutine(async () =>
        {
            var connector = new ScriptedConnector(new S_PlayerJoined { Success = false, Message = "Room is full" });
            var session = new SocketSession(connector, new NoopDispatcher());

            await session.ConnectAsync(Info(), CancellationToken.None);
            await UniTask.WaitUntil(() => session.State == SocketSessionState.Failed);

            Assert.AreEqual("Room is full", session.LastJoinFailureReason,
                "거절 사유를 버리면 30번 재시도해도 원인을 알 수 없다.");

            await session.DisconnectAsync(CancellationToken.None);
        });

        [UnityTest]
        public IEnumerator 입장_성공하면_이전_사유가_지워진다() => UniTask.ToCoroutine(async () =>
        {
            var connector = new ScriptedConnector(new S_PlayerJoined { Success = true, UserId = 1 });
            var session = new SocketSession(connector, new NoopDispatcher());

            await session.ConnectAsync(Info(), CancellationToken.None);
            await UniTask.WaitUntil(() => session.State == SocketSessionState.Joined);

            Assert.IsNull(session.LastJoinFailureReason, "성공 후에도 옛 사유가 남으면 다음 진단이 오염된다.");

            await session.DisconnectAsync(CancellationToken.None);
        });
    }
}
