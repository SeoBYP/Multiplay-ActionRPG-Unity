using System.Threading;
using System.Threading.Tasks;
using Game.Network.Socket;
using Game.Network.Socket.Packets;
using NUnit.Framework;

namespace Game.Tests.EditMode.Socket
{
    /// <summary>
    /// 저수준 커넥터의 송신↔종료 예외 안전성. 실게임에서 던전 이탈/소켓 드롭 순간 `MoveSyncSender` 가
    /// C_Move 를 마지막으로 한 번 더 쏘면서 `SendAsync` 가 끊긴 스트림을 만나 NRE 를 던지던 회귀를 막는다.
    /// (SocketConnector.SendAsync: IsConnected 통과 후 _sendLock 대기 중 DisconnectAsync 가 _stream=null →
    ///  로컬 캡처 + 널체크 + IsExpectedDisconnect 로 조용히 무시.)
    ///
    /// 정확한 "락 대기 중 널 교체" 타이밍 레이스는 결정론적 단위재현이 어렵다 → 관찰 가능한 계약
    /// (미연결/끊김 상태 송신은 예외를 던지지 않는다)을 잠근다. 연결 생명주기 전반은 SocketE2ETests(Docker).
    /// </summary>
    public class SocketConnectorTests
    {
        [Test]
        public async Task SendAsync_는_미연결_상태에서_예외없이_무시된다()
        {
            var connector = new SocketConnector();
            Assert.IsFalse(connector.IsConnected, "연결한 적 없으므로 미연결이어야 한다.");

            await connector.SendAsync(new C_Move { PosX = 1f, PosZ = 2f }, CancellationToken.None);

            Assert.IsFalse(connector.IsConnected);
        }

        [Test]
        public async Task SendAsync_는_Disconnect_후에도_예외없이_무시된다()
        {
            var connector = new SocketConnector();
            await connector.DisconnectAsync(CancellationToken.None); // 연결 없이 정리(멱등)

            await connector.SendAsync(new C_Move { PosX = 3f }, CancellationToken.None);

            Assert.IsFalse(connector.IsConnected);
        }
    }
}
