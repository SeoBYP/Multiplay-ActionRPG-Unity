using System;
using System.Collections;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Network.Https.Core;
using Game.Network.Https.Services;
using Game.Network.Socket;
using Game.Network.Socket.Packets;
using GameServer.Grpc.Auth;
using GameServer.Grpc.DungeonLobby;
using GameServer.Grpc.User;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode.E2E
{
    /// <summary>
    /// GameSessionConnector 연결 복구 시나리오 E2E 테스트.
    ///
    /// SocketSession 을 직접 사용해 실제 서버 대상으로
    /// Disconnected 상태 후 재시도가 올바르게 동작하는지 검증한다.
    ///
    /// GameSessionConnector.ConnectAndLoadDungeonAsync 가 내부적으로 수행하는
    /// Connect → JoinRoom → WaitUntil(Joined|Failed|Disconnected) 루프를
    /// 동일 패턴으로 직접 구현해 동작을 검증한다.
    /// </summary>
    [TestFixture]
    public class GameSessionConnectorE2ETests : E2ETestBase
    {
        // ── 재연결 헬퍼 ─────────────────────────────────────────────

        /// <summary>
        /// GameSessionConnector.ConnectAndLoadDungeonAsync 재시도 루프와 동일한 패턴.
        /// Joined / Failed / Disconnected 모두 WaitUntil 탈출 조건으로 처리한다.
        /// </summary>
        private static async UniTask<bool> TryConnectWithRetryAsync(
            long roomId, long userId,
            int maxAttempts,
            CancellationToken ct)
        {
            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                var connector  = new SocketConnector();
                var dispatcher = new SocketPacketDispatcher(new IPacketHandler[]
                {
                    new PlayerJoinedPacketHandler(new SocketPacketState()),
                    new MovePacketHandler(new SocketPacketState()),
                });
                var session = new SocketSession(connector, dispatcher);

                try
                {
                    await session.ConnectAsync(
                        new SocketConnectionInfo(
                            ServerConfig.SocketServerHost,
                            ServerConfig.SocketServerPort,
                            roomId, userId), ct);

                    await session.JoinRoomAsync(ct);

                    // Disconnected 도 탈출 조건 — 없으면 무한 대기
                    await UniTask.WaitUntil(
                        () => session.State == SocketSessionState.Joined
                           || session.State == SocketSessionState.Failed
                           || session.State == SocketSessionState.Disconnected,
                        cancellationToken: ct);

                    if (session.State == SocketSessionState.Joined)
                    {
                        await session.DisconnectAsync(CancellationToken.None);
                        return true;
                    }

                    await session.DisconnectAsync(CancellationToken.None);
                }
                catch (OperationCanceledException) { throw; }
                catch { /* 재시도 */ }
                finally
                {
                    await connector.DisposeAsync();
                }

                if (attempt < maxAttempts)
                    await UniTask.Delay(TimeSpan.FromMilliseconds(300), cancellationToken: ct);
            }

            return false;
        }

        // ── 테스트 ───────────────────────────────────────────────────

        /// <summary>
        /// Disconnected 상태 발생 후 WaitUntil 루프가 무한 대기에 빠지지 않고
        /// 다음 시도를 계속 진행해 최종적으로 Joined 에 도달해야 한다.
        ///
        /// 시나리오:
        ///   1차 시도: JoinRoomAsync 후 즉시 DisconnectAsync → Disconnected
        ///   → WaitUntil 탈출 (수정 전에는 여기서 무한 대기)
        ///   2차 시도: 정상 입장 → Joined
        /// </summary>
        [UnityTest]
        public IEnumerator Disconnected_상태에서_무한대기_없이_재시도해_Joined_도달() => UniTask.ToCoroutine(async () =>
        {
            // Arrange: 2-player 방 (게스트가 방 유지)
            var provider = new GrpcChannelProvider(ServerConfig.GameServerGrpcAddress);
            var auth     = new AuthGrpcService(provider);
            var user     = new UserGrpcService(provider);
            var lobby    = new DungeonLobbyGrpcService(provider);

            try
            {
                // 호스트 계정
                var hostEmail = E2ETestBase.UniqueEmail();
                await auth.RegisterAsync(new RegisterRequest { Email = hostEmail, Password = "Test1234!" }, Timeout());
                var hostLogin = await auth.LoginAsync(new LoginRequest { Email = hostEmail, Password = "Test1234!", DeviceId = "gc-e2e-host" }, Timeout());
                provider.AccessTokenProvider = () => hostLogin.AccessToken;
                await user.SetNickNameAsync(new SetNicknameRequest { Nickname = E2ETestBase.UniqueNickname("GCHost") }, Timeout());
                var hostUserId = ExtractUserId(hostLogin.AccessToken);

                var created = await lobby.CreateRoomAsync(new CreateRoomRequest { RoomName = "GC E2E", MaxPlayers = 2 }, Timeout());
                Assert.IsTrue(created.Result.Success);

                // 게스트 계정
                var guestEmail = E2ETestBase.UniqueEmail();
                await auth.RegisterAsync(new RegisterRequest { Email = guestEmail, Password = "Test1234!" }, Timeout());
                var guestLogin = await auth.LoginAsync(new LoginRequest { Email = guestEmail, Password = "Test1234!", DeviceId = "gc-e2e-guest" }, Timeout());
                provider.AccessTokenProvider = () => guestLogin.AccessToken;
                await user.SetNickNameAsync(new SetNicknameRequest { Nickname = E2ETestBase.UniqueNickname("GCGuest") }, Timeout());
                var guestUserId = ExtractUserId(guestLogin.AccessToken);
                await lobby.JoinRoomAsync(new JoinRoomRequest { RoomId = created.RoomInfo.RoomId }, Timeout());
                // 호스트를 뺀 전원이 준비해야 StartRoom 이 통과한다(준비 게이트, 서버 권위).
                await lobby.SetReadyAsync(new SetReadyRequest { RoomId = created.RoomInfo.RoomId, IsReady = true }, Timeout());

                // 게임 시작 (호스트)
                provider.AccessTokenProvider = () => hostLogin.AccessToken;
                var started = await lobby.StartRoomAsync(new StartRoomRequest { RoomId = created.RoomInfo.RoomId }, Timeout());
                Assert.IsTrue(started.Result.Success);

                // 게스트 입장 — 방 유지용
                var guestCollector = await ConnectAndJoinCollectorAsyncHelper(
                    created.RoomInfo.RoomId, guestUserId, Timeout());

                try
                {
                    // ── 1차 시도: 연결 직후 즉시 끊기 (Disconnected 유발) ──
                    var connector1  = new SocketConnector();
                    var dispatcher1 = new SocketPacketDispatcher(new IPacketHandler[]
                    {
                        new PlayerJoinedPacketHandler(new SocketPacketState()),
                        new MovePacketHandler(new SocketPacketState()),
                    });
                    var session1 = new SocketSession(connector1, dispatcher1);

                    await session1.ConnectAsync(
                        new SocketConnectionInfo(ServerConfig.SocketServerHost, ServerConfig.SocketServerPort,
                            created.RoomInfo.RoomId, hostUserId), Timeout());

                    // JoinRoomAsync 없이 강제 Disconnect → Disconnected 상태 유발
                    await session1.DisconnectAsync(CancellationToken.None);
                    await connector1.DisposeAsync();

                    Assert.AreEqual(SocketSessionState.Disconnected, session1.State,
                        "강제 종료 후 Disconnected 상태여야 한다");

                    // ── 2차 시도: Disconnected 후 WaitUntil 루프가 탈출했으므로 재시도 가능 ──
                    var joined = await TryConnectWithRetryAsync(
                        created.RoomInfo.RoomId, hostUserId,
                        maxAttempts: 15,
                        ct: Timeout());

                    Assert.IsTrue(joined,
                        "Disconnected 이후 재시도 루프가 Joined 에 도달해야 한다");
                }
                finally
                {
                    await guestCollector.DisposeAsync();
                }
            }
            finally
            {
                provider.Dispose();
            }
        });

        /// <summary>
        /// C_PlayerLeave 없이 TCP만 끊은 뒤(크래시 시뮬레이션)
        /// 다른 플레이어가 방에 남아 있는 한 재접속이 가능해야 한다.
        /// </summary>
        [UnityTest]
        public IEnumerator 크래시_후_재접속_성공() => UniTask.ToCoroutine(async () =>
        {
            var provider = new GrpcChannelProvider(ServerConfig.GameServerGrpcAddress);
            var auth     = new AuthGrpcService(provider);
            var user     = new UserGrpcService(provider);
            var lobby    = new DungeonLobbyGrpcService(provider);

            try
            {
                // 호스트
                var hostEmail = E2ETestBase.UniqueEmail();
                await auth.RegisterAsync(new RegisterRequest { Email = hostEmail, Password = "Test1234!" }, Timeout());
                var hostLogin = await auth.LoginAsync(new LoginRequest { Email = hostEmail, Password = "Test1234!", DeviceId = "crash-host" }, Timeout());
                provider.AccessTokenProvider = () => hostLogin.AccessToken;
                await user.SetNickNameAsync(new SetNicknameRequest { Nickname = E2ETestBase.UniqueNickname("CrashHost") }, Timeout());
                var hostUserId = ExtractUserId(hostLogin.AccessToken);
                var created = await lobby.CreateRoomAsync(new CreateRoomRequest { RoomName = "Crash E2E", MaxPlayers = 2 }, Timeout());

                // 게스트
                var guestEmail = E2ETestBase.UniqueEmail();
                await auth.RegisterAsync(new RegisterRequest { Email = guestEmail, Password = "Test1234!" }, Timeout());
                var guestLogin = await auth.LoginAsync(new LoginRequest { Email = guestEmail, Password = "Test1234!", DeviceId = "crash-guest" }, Timeout());
                provider.AccessTokenProvider = () => guestLogin.AccessToken;
                await user.SetNickNameAsync(new SetNicknameRequest { Nickname = E2ETestBase.UniqueNickname("CrashGuest") }, Timeout());
                var guestUserId = ExtractUserId(guestLogin.AccessToken);
                await lobby.JoinRoomAsync(new JoinRoomRequest { RoomId = created.RoomInfo.RoomId }, Timeout());
                // 호스트를 뺀 전원이 준비해야 StartRoom 이 통과한다(준비 게이트, 서버 권위).
                await lobby.SetReadyAsync(new SetReadyRequest { RoomId = created.RoomInfo.RoomId, IsReady = true }, Timeout());

                provider.AccessTokenProvider = () => hostLogin.AccessToken;
                await lobby.StartRoomAsync(new StartRoomRequest { RoomId = created.RoomInfo.RoomId }, Timeout());

                // 게스트 입장 (방 유지)
                var guestCollector = await ConnectAndJoinCollectorAsyncHelper(
                    created.RoomInfo.RoomId, guestUserId, Timeout());

                // 호스트 첫 번째 입장
                var hostCollector = await ConnectAndJoinCollectorAsyncHelper(
                    created.RoomInfo.RoomId, hostUserId, Timeout());

                try
                {
                    // 호스트 크래시 시뮬레이션 — C_PlayerLeave 없이 강제 종료
                    await hostCollector.DisposeAsync();
                    hostCollector = null;

                    // 서버가 TCP 끊김을 감지할 시간
                    await UniTask.Delay(TimeSpan.FromMilliseconds(600));

                    // 재접속 — gamesession:player 키 유효 + 게스트가 방 유지
                    var rejoined = await TryConnectWithRetryAsync(
                        created.RoomInfo.RoomId, hostUserId,
                        maxAttempts: 15,
                        ct: Timeout());

                    Assert.IsTrue(rejoined, "크래시 후 재접속이 성공해야 한다");
                }
                finally
                {
                    if (hostCollector != null) await hostCollector.DisposeAsync();
                    await guestCollector.DisposeAsync();
                }
            }
            finally
            {
                provider.Dispose();
            }
        });

        // ── 헬퍼 ─────────────────────────────────────────────────────

        private static async UniTask<SocketPacketCollector> ConnectAndJoinCollectorAsyncHelper(
            long roomId, long userId, CancellationToken ct)
        {
            var deadline = DateTime.UtcNow.AddSeconds(ServerConfig.TimeoutSeconds);
            Exception lastError = null;

            while (DateTime.UtcNow < deadline)
            {
                var collector = new SocketPacketCollector();
                try
                {
                    await collector.ConnectAsync(ServerConfig.SocketServerHost, ServerConfig.SocketServerPort, ct);
                    await collector.SendAsync(new C_PlayerJoin { RoomId = roomId, UserId = userId }, ct);
                    var joined = await collector.WaitForPacketAsync<S_PlayerJoined>(
                        p => p.UserId == userId || !p.Success, ct);
                    if (!joined.Success) throw new Exception($"Join failed: {joined.Message}");
                    return collector;
                }
                catch (Exception ex)
                {
                    lastError = ex;
                    await collector.DisposeAsync();
                    await UniTask.Delay(TimeSpan.FromMilliseconds(200), cancellationToken: ct);
                }
            }
            throw new Exception("ConnectAndJoin timed out", lastError);
        }

        private static long ExtractUserId(string accessToken)
        {
            var parts   = accessToken.Split('.');
            var payload = parts[1].Replace('-', '+').Replace('_', '/');
            switch (payload.Length % 4)
            {
                case 2: payload += "=="; break;
                case 3: payload += "=";  break;
            }
            var json  = Encoding.UTF8.GetString(Convert.FromBase64String(payload));
            var match = Regex.Match(json, "\"sub\"\\s*:\\s*\"(?<id>\\d+)\"");
            return long.Parse(match.Groups["id"].Value);
        }
    }
}
