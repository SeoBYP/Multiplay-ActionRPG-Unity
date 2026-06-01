using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
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
    [TestFixture]
    public class SocketE2ETests : E2ETestBase
    {
        [UnityTest]
        public IEnumerator SocketSession_두_클라이언트_인증후_입장_성공() => UniTask.ToCoroutine(async () =>
        {
            var room = await CreateStartedTwoPlayerRoomAsync();
            var hostClient = await ConnectJoinedSessionAsync(room.RoomId, room.HostUserId, Timeout());
            var guestClient = await ConnectJoinedSessionAsync(room.RoomId, room.GuestUserId, Timeout());

            try
            {
                Assert.AreEqual(SocketSessionState.Joined, hostClient.Session.State);
                Assert.IsTrue(hostClient.State.TryGetPlayer(room.HostUserId, out var hostPlayer));
                Assert.AreEqual(room.HostNickname, hostPlayer.Nickname);
                
                Assert.AreEqual(SocketSessionState.Joined, guestClient.Session.State);
                Assert.IsTrue(guestClient.State.TryGetPlayer(room.GuestUserId, out var guestPlayer));
                Assert.AreEqual(room.GuestNickname, guestPlayer.Nickname);
            }
            finally
            {
                await hostClient.Session.DisconnectAsync(CancellationToken.None);
                await guestClient.Session.DisconnectAsync(CancellationToken.None);
                await hostClient.Connector.DisposeAsync();
                await guestClient.Connector.DisposeAsync();
            }
        });

        [UnityTest]
        public IEnumerator RawSocket_호스트가_Move_전송하면_게스트가_S_Move_수신() => UniTask.ToCoroutine(async () =>
        {
            var room = await CreateStartedTwoPlayerRoomAsync();
            var hostCollector = await ConnectAndJoinCollectorAsync(room.RoomId, room.HostUserId, Timeout());
            var guestCollector = await ConnectAndJoinCollectorAsync(room.RoomId, room.GuestUserId, Timeout());

            try
            {
                const float moveX = 10f;
                const float moveY = 0f;
                const float moveZ = 5f;
                const float rotY = 90f;

                await hostCollector.SendAsync(new C_Move
                {
                    PosX = moveX,
                    PosY = moveY,
                    PosZ = moveZ,
                    RotY = rotY
                }, Timeout());

                var move = await guestCollector.WaitForPacketAsync<S_Move>(
                    packet => packet.UserId == room.HostUserId
                           && packet.PosX == moveX
                           && packet.PosY == moveY
                           && packet.PosZ == moveZ
                           && packet.RotY == rotY,
                    Timeout());

                Assert.AreEqual(room.HostUserId, move.UserId);
                Assert.AreEqual(moveX, move.PosX);
                Assert.AreEqual(moveY, move.PosY);
                Assert.AreEqual(moveZ, move.PosZ);
                Assert.AreEqual(rotY, move.RotY);
            }
            finally
            {
                await hostCollector.DisposeAsync();
                await guestCollector.DisposeAsync();
            }
        });

        [UnityTest]
        public IEnumerator RawSocket_호스트가_퇴장하면_게스트가_S_PlayerLeft_수신() => UniTask.ToCoroutine(async () =>
        {
            var room = await CreateStartedTwoPlayerRoomAsync();
            var hostCollector = await ConnectAndJoinCollectorAsync(room.RoomId, room.HostUserId, Timeout());
            var guestCollector = await ConnectAndJoinCollectorAsync(room.RoomId, room.GuestUserId, Timeout());

            try
            {
                // 호스트가 C_PlayerLeave 전송 → 서버 RoomManager.LeaveRoom →
                // 남은 게스트에게 S_PlayerLeft{UserId=hostUserId} 브로드캐스트.
                await hostCollector.SendAsync(new C_PlayerLeave(), Timeout());

                var left = await guestCollector.WaitForPacketAsync<S_PlayerLeft>(
                    packet => packet.UserId == room.HostUserId,
                    Timeout());

                Assert.AreEqual(room.HostUserId, left.UserId);
            }
            finally
            {
                await hostCollector.DisposeAsync();
                await guestCollector.DisposeAsync();
            }
        });

        // ── 재연결 / Disconnected 상태 시나리오 ────────────────────────

        /// <summary>
        /// C_PlayerLeave 없이 강제 연결 종료(게임 크래시 시뮬레이션) 후
        /// 같은 방에 다시 입장할 수 있어야 한다.
        ///
        /// 조건: 방에 다른 플레이어(게스트)가 남아 있어 SocketServer 방이 유지됨.
        /// gamesession:player 키는 2시간 TTL → 재접속에 사용 가능.
        /// </summary>
        [UnityTest]
        public IEnumerator 강제_연결_끊김_후_재접속_성공() => UniTask.ToCoroutine(async () =>
        {
            var room = await CreateStartedTwoPlayerRoomAsync();

            // 게스트 먼저 입장 (방 유지 앵커)
            var guestCollector = await ConnectAndJoinCollectorAsync(room.RoomId, room.GuestUserId, Timeout());
            // 호스트 첫 번째 입장
            var hostCollector = await ConnectAndJoinCollectorAsync(room.RoomId, room.HostUserId, Timeout());

            try
            {
                // 호스트 강제 종료 — C_PlayerLeave 없이 TCP 연결만 끊는다 (크래시 시뮬레이션)
                await hostCollector.DisposeAsync();
                hostCollector = null;

                // 서버가 TCP 연결 끊김을 감지할 시간을 준다
                await UniTask.Delay(TimeSpan.FromMilliseconds(600));

                // 호스트 재접속 시도 — gamesession:player 키가 유효하고
                // 게스트가 방을 유지 중이므로 재입장 가능해야 한다
                hostCollector = await ConnectAndJoinCollectorAsync(room.RoomId, room.HostUserId, Timeout());

                Assert.IsNotNull(hostCollector, "강제 끊김 후 재접속이 성공해야 한다");
            }
            finally
            {
                if (hostCollector != null)  await hostCollector.DisposeAsync();
                await guestCollector.DisposeAsync();
            }
        });

        /// <summary>
        /// SocketSession 이 Disconnected 상태가 되어도 재접속 루프가
        /// 무한 대기에 빠지지 않고 다음 시도를 진행해야 한다.
        ///
        /// 시나리오:
        ///   1회 시도: TCP 연결 직후 강제 종료 → Session.State = Disconnected
        ///   2회 시도: 정상 입장 → Joined
        /// </summary>
        [UnityTest]
        public IEnumerator Disconnected_상태에서_재시도로_입장_성공() => UniTask.ToCoroutine(async () =>
        {
            var room = await CreateStartedTwoPlayerRoomAsync();

            // 게스트가 방을 유지
            var guestCollector = await ConnectAndJoinCollectorAsync(room.RoomId, room.GuestUserId, Timeout());

            try
            {
                // --- 1차 시도: 연결 후 즉시 Disconnect (응답 대기 없이 끊음) ---
                var state1    = new SocketPacketState();
                var connector1 = new SocketConnector();
                var dispatcher1 = new SocketPacketDispatcher(new IPacketHandler[]
                    { new PlayerJoinedPacketHandler(state1), new MovePacketHandler(state1) });
                var session1 = new SocketSession(connector1, dispatcher1);

                await session1.ConnectAsync(
                    new SocketConnectionInfo(ServerConfig.SocketServerHost, ServerConfig.SocketServerPort,
                        room.RoomId, room.HostUserId), Timeout());

                // JoinRoomAsync 없이 강제 Disconnect → State = Disconnected
                await session1.DisconnectAsync(CancellationToken.None);
                await connector1.DisposeAsync();

                Assert.AreEqual(SocketSessionState.Disconnected, session1.State,
                    "강제 종료 후 Disconnected 상태여야 한다");

                // --- 2차 시도: 정상 입장 ---
                // Disconnected 상태에서 새 세션을 만들어 재시도하면 성공해야 한다
                var hostCollector = await ConnectAndJoinCollectorAsync(room.RoomId, room.HostUserId, Timeout());
                Assert.IsNotNull(hostCollector, "Disconnected 이후 재시도로 입장 성공해야 한다");
                await hostCollector.DisposeAsync();
            }
            finally
            {
                await guestCollector.DisposeAsync();
            }
        });

        [UnityTest]
        public IEnumerator 게스트_부분퇴장후_재로그인시_방복원_안되고_호스트는_유지() => UniTask.ToCoroutine(async () =>
        {
            var room = await CreateStartedTwoPlayerRoomAsync();
            var hostCollector = await ConnectAndJoinCollectorAsync(room.RoomId, room.HostUserId, Timeout());
            var guestCollector = await ConnectAndJoinCollectorAsync(room.RoomId, room.GuestUserId, Timeout());

            try
            {
                // 게스트만 명시적 퇴장 (방엔 호스트가 남음 = 부분 퇴장)
                await guestCollector.SendAsync(new C_PlayerLeave(), Timeout());

                // 호스트가 S_PlayerLeft(게스트) 수신 — 퇴장 전파 확인
                await hostCollector.WaitForPacketAsync<S_PlayerLeft>(
                    packet => packet.UserId == room.GuestUserId, Timeout());

                // GameServer가 PlayerLeft를 소비해 게스트 association을 제거할 때까지 폴링
                // (재로그인 응답의 CurrentRoomId==0 이면 복원 안 됨).
                long guestRoomId = -1;
                var deadline = DateTime.UtcNow.AddSeconds(ServerConfig.TimeoutSeconds);
                while (DateTime.UtcNow < deadline)
                {
                    guestRoomId = await ReloginCurrentRoomIdAsync(room.GuestEmail, "socket-e2e-guest-device", Timeout());
                    if (guestRoomId == 0) break;
                    await UniTask.Delay(TimeSpan.FromMilliseconds(200));
                }

                Assert.AreEqual(0, guestRoomId, "퇴장한 게스트는 재로그인 시 방으로 복원되면 안 된다");

                // 남은 호스트는 여전히 그 방으로 복원 가능해야 한다 (방 유지).
                var hostRoomId = await ReloginCurrentRoomIdAsync(room.HostEmail, "socket-e2e-host-device", Timeout());
                Assert.AreEqual(room.RoomId, hostRoomId, "남은 호스트는 방 복원 가능해야 한다");
            }
            finally
            {
                await hostCollector.DisposeAsync();
                await guestCollector.DisposeAsync();
            }
        });

        private static async UniTask<long> ReloginCurrentRoomIdAsync(string email, string deviceId, CancellationToken ct)
        {
            var provider = new GrpcChannelProvider(ServerConfig.GameServerGrpcAddress);
            try
            {
                var authService = new AuthGrpcService(provider);
                var login = await authService.LoginAsync(new LoginRequest
                {
                    Email = email,
                    Password = "Test1234!",
                    DeviceId = deviceId
                }, ct);
                Assert.IsTrue(login.Result.Success, login.Result.Message);
                return login.User.CurrentRoomId;
            }
            finally
            {
                provider.Dispose();
            }
        }

        private static async UniTask<StartedRoomContext> CreateStartedTwoPlayerRoomAsync()
        {
            var provider = new GrpcChannelProvider(ServerConfig.GameServerGrpcAddress);

            try
            {
                var authService = new AuthGrpcService(provider);
                var userService = new UserGrpcService(provider);
                var lobbyService = new DungeonLobbyGrpcService(provider);

                var hostEmail = UniqueEmail();
                var hostNickname = UniqueNickname("Host");

                var hostRegister = await authService.RegisterAsync(new RegisterRequest
                {
                    Email = hostEmail,
                    Password = "Test1234!"
                }, Timeout());
                Assert.IsTrue(hostRegister.Result.Success, hostRegister.Result.Message);

                var hostLogin = await authService.LoginAsync(new LoginRequest
                {
                    Email = hostEmail,
                    Password = "Test1234!",
                    DeviceId = "socket-e2e-host-device"
                }, Timeout());
                Assert.IsTrue(hostLogin.Result.Success, hostLogin.Result.Message);

                provider.AccessTokenProvider = () => hostLogin.AccessToken;

                var setHostNickname = await userService.SetNickNameAsync(new SetNicknameRequest
                {
                    Nickname = hostNickname
                }, Timeout());
                Assert.IsTrue(setHostNickname.Result.Success, setHostNickname.Result.Message);

                var hostUserId = ExtractUserIdFromAccessToken(hostLogin.AccessToken);

                var created = await lobbyService.CreateRoomAsync(new CreateRoomRequest
                {
                    RoomName = $"Socket E2E {hostNickname}",
                    MaxPlayers = 2
                }, Timeout());
                Assert.IsTrue(created.Result.Success, created.Result.Message);

                var guestEmail = UniqueEmail();
                var guestNickname = UniqueNickname("Guest");

                var guestRegister = await authService.RegisterAsync(new RegisterRequest
                {
                    Email = guestEmail,
                    Password = "Test1234!"
                }, Timeout());
                Assert.IsTrue(guestRegister.Result.Success, guestRegister.Result.Message);

                var guestLogin = await authService.LoginAsync(new LoginRequest
                {
                    Email = guestEmail,
                    Password = "Test1234!",
                    DeviceId = "socket-e2e-guest-device"
                }, Timeout());
                Assert.IsTrue(guestLogin.Result.Success, guestLogin.Result.Message);

                provider.AccessTokenProvider = () => guestLogin.AccessToken;

                var setGuestNickname = await userService.SetNickNameAsync(new SetNicknameRequest
                {
                    Nickname = guestNickname
                }, Timeout());
                Assert.IsTrue(setGuestNickname.Result.Success, setGuestNickname.Result.Message);

                var guestUserId = ExtractUserIdFromAccessToken(guestLogin.AccessToken);

                var joined = await lobbyService.JoinRoomAsync(new JoinRoomRequest
                {
                    RoomId = created.RoomInfo.RoomId
                }, Timeout());
                Assert.IsTrue(joined.Result.Success, joined.Result.Message);

                provider.AccessTokenProvider = () => hostLogin.AccessToken;

                var started = await lobbyService.StartRoomAsync(new StartRoomRequest
                {
                    RoomId = created.RoomInfo.RoomId
                }, Timeout());
                Assert.IsTrue(started.Result.Success, started.Result.Message);

                return new StartedRoomContext(
                    created.RoomInfo.RoomId,
                    hostUserId,
                    guestUserId,
                    hostNickname,
                    guestNickname,
                    hostEmail,
                    guestEmail);
            }
            finally
            {
                provider.Dispose();
            }
        }

        private static async UniTask<SocketClientContext> ConnectJoinedSessionAsync(long roomId, long userId, CancellationToken ct)
        {
            Exception lastError = null;
            var deadline = DateTime.UtcNow.AddSeconds(ServerConfig.TimeoutSeconds);

            while (DateTime.UtcNow < deadline)
            {
                var state = new SocketPacketState();
                var connector = new SocketConnector();
                var dispatcher = new SocketPacketDispatcher(new IPacketHandler[]
                {
                    new PlayerJoinedPacketHandler(state),
                    new MovePacketHandler(state)
                });
                var session = new SocketSession(connector, dispatcher);

                try
                {
                    await session.ConnectAsync(
                        new SocketConnectionInfo(ServerConfig.SocketServerHost, ServerConfig.SocketServerPort, roomId, userId),
                        ct);

                    await session.JoinRoomAsync(ct);
                    await UniTask.WaitUntil(
                        () => state.TryGetPlayer(userId, out _) || session.State == SocketSessionState.Failed,
                        cancellationToken: ct);

                    if (!state.TryGetPlayer(userId, out _))
                    {
                        throw new InvalidOperationException("Player join did not complete successfully.");
                    }

                    return new SocketClientContext(session, connector, state);
                }
                catch (Exception ex)
                {
                    lastError = ex;

                    try
                    {
                        await session.DisconnectAsync(CancellationToken.None);
                    }
                    catch
                    {
                    }

                    await connector.DisposeAsync();
                    await UniTask.Delay(TimeSpan.FromMilliseconds(200), cancellationToken: ct);
                }
            }

            throw new InvalidOperationException("Failed to connect and join socket session.", lastError);
        }

        private static async UniTask<SocketPacketCollector> ConnectAndJoinCollectorAsync(long roomId, long userId, CancellationToken ct)
        {
            Exception lastError = null;
            var deadline = DateTime.UtcNow.AddSeconds(ServerConfig.TimeoutSeconds);

            while (DateTime.UtcNow < deadline)
            {
                var collector = new SocketPacketCollector();

                try
                {
                    await collector.ConnectAsync(ServerConfig.SocketServerHost, ServerConfig.SocketServerPort, ct);
                    await collector.SendAsync(new C_PlayerJoin { RoomId = roomId, UserId = userId }, ct);

                    // 실패 응답(Success=false)은 UserId=0으로 오므로 UserId 매칭만으로는
                    // 관측되지 않는다. 방 생성(스트림 소비)과 클라 접속 사이 레이스로
                    // "Room not found"가 올 수 있어, 실패 패킷도 관측해 재시도 경로를 태운다.
                    var joined = await collector.WaitForPacketAsync<S_PlayerJoined>(
                        packet => packet.UserId == userId || !packet.Success,
                        ct);

                    if (!joined.Success)
                    {
                        throw new InvalidOperationException($"Join failed: {joined.Message}");
                    }

                    return collector;
                }
                catch (Exception ex)
                {
                    lastError = ex;
                    await collector.DisposeAsync();
                    await UniTask.Delay(TimeSpan.FromMilliseconds(200), cancellationToken: ct);
                }
            }

            throw new InvalidOperationException("Failed to connect and join raw socket client.", lastError);
        }

        private static long ExtractUserIdFromAccessToken(string accessToken)
        {
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                throw new ArgumentException("Access token is empty.", nameof(accessToken));
            }

            var parts = accessToken.Split('.');
            if (parts.Length < 2)
            {
                throw new InvalidOperationException("Access token payload is invalid.");
            }

            var payload = parts[1].Replace('-', '+').Replace('_', '/');

            switch (payload.Length % 4)
            {
                case 2:
                    payload += "==";
                    break;
                case 3:
                    payload += "=";
                    break;
            }

            var json = Encoding.UTF8.GetString(Convert.FromBase64String(payload));
            var match = Regex.Match(json, "\"sub\"\\s*:\\s*\"(?<id>\\d+)\"");

            if (!match.Success)
            {
                throw new InvalidOperationException("User id claim was not found in access token.");
            }

            return long.Parse(match.Groups["id"].Value);
        }

        private sealed class SocketPacketCollector
        {
            private readonly List<Packet> _receivedPackets = new List<Packet>();
            private readonly object _sync = new object();
            private readonly SocketConnector _connector = new SocketConnector();

            private CancellationTokenSource _receiveCts;
            private UniTask _receiveLoop;

            public async UniTask ConnectAsync(string host, int port, CancellationToken ct)
            {
                _receiveCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                await _connector.ConnectAsync(host, port, _receiveCts.Token);
                _receiveLoop = _connector.StartReceiveLoopAsync(OnPacketAsync, _receiveCts.Token);
                _receiveLoop.Forget(ex =>
                {
                    if (!IsExpectedDisconnectException(ex))
                    {
                        UnityEngine.Debug.LogException(ex);
                    }
                });
            }

            public UniTask SendAsync(Packet packet, CancellationToken ct)
            {
                return _connector.SendAsync(packet, ct);
            }

            public async UniTask<TPacket> WaitForPacketAsync<TPacket>(Func<TPacket, bool> predicate, CancellationToken ct)
                where TPacket : Packet
            {
                await UniTask.WaitUntil(() => TryFindPacket(predicate, out TPacket _), cancellationToken: ct);
                TryFindPacket(predicate, out TPacket packet);
                return packet;
            }

            public async UniTask DisposeAsync()
            {
                _receiveCts?.Cancel();
                _receiveCts?.Dispose();
                await _connector.DisposeAsync();
            }

            private UniTask OnPacketAsync(Packet packet)
            {
                lock (_sync)
                {
                    _receivedPackets.Add(packet);
                }

                return UniTask.CompletedTask;
            }

            private bool TryFindPacket<TPacket>(Func<TPacket, bool> predicate, out TPacket found)
                where TPacket : Packet
            {
                lock (_sync)
                {
                    foreach (var packet in _receivedPackets)
                    {
                        if (packet is TPacket typed && predicate(typed))
                        {
                            found = typed;
                            return true;
                        }
                    }
                }

                found = null;
                return false;
            }

            private static bool IsExpectedDisconnectException(Exception exception)
            {
                if (exception is OperationCanceledException)
                {
                    return true;
                }

                if (exception is ObjectDisposedException)
                {
                    return true;
                }

                if (exception is IOException ioException && ioException.InnerException is SocketException)
                {
                    return true;
                }

                return exception is SocketException;
            }
        }

        private sealed class StartedRoomContext
        {
            public long RoomId { get; }
            public long HostUserId { get; }
            public long GuestUserId { get; }
            public string HostNickname { get; }
            public string GuestNickname { get; }
            public string HostEmail { get; }
            public string GuestEmail { get; }

            public StartedRoomContext(
                long roomId,
                long hostUserId,
                long guestUserId,
                string hostNickname,
                string guestNickname,
                string hostEmail,
                string guestEmail)
            {
                RoomId = roomId;
                HostUserId = hostUserId;
                GuestUserId = guestUserId;
                HostNickname = hostNickname;
                GuestNickname = guestNickname;
                HostEmail = hostEmail;
                GuestEmail = guestEmail;
            }
        }

        private sealed class SocketClientContext
        {
            public ISocketSession Session { get; }
            public SocketConnector Connector { get; }
            public SocketPacketState State { get; }

            public SocketClientContext(ISocketSession session, SocketConnector connector, SocketPacketState state)
            {
                Session = session;
                Connector = connector;
                State = state;
            }
        }
    }
}
