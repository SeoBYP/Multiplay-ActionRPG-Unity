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
                Assert.IsTrue(hostClient.State.IsAuthenticated);
                Assert.AreEqual(SocketSessionState.Joined, hostClient.Session.State);
                Assert.IsTrue(hostClient.State.TryGetPlayer(room.HostUserId, out var hostPlayer));
                Assert.AreEqual(room.HostNickname, hostPlayer.Nickname);

                Assert.IsTrue(guestClient.State.IsAuthenticated);
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
                    guestNickname);
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
                    new AuthPacketHandler(state),
                    new PlayerJoinedPacketHandler(state),
                    new MovePacketHandler(state)
                });
                var session = new SocketSession(connector, dispatcher);

                try
                {
                    await session.ConnectAsync(
                        new SocketConnectionInfo(ServerConfig.SocketServerHost, ServerConfig.SocketServerPort, roomId, userId),
                        ct);

                    await session.AuthenticateAsync(ct);
                    await UniTask.WaitUntil(
                        () => state.IsAuthenticated || session.State == SocketSessionState.Failed,
                        cancellationToken: ct);

                    if (!state.IsAuthenticated)
                    {
                        throw new InvalidOperationException("Authentication did not complete successfully.");
                    }

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
                    await collector.SendAsync(new C_Auth { UserId = userId }, ct);

                    var auth = await collector.WaitForPacketAsync<S_Auth>(_ => true, ct);
                    if (!auth.Success)
                    {
                        throw new InvalidOperationException($"Authentication failed: {auth.Message}");
                    }

                    await collector.SendAsync(new C_PlayerJoin { RoomId = roomId }, ct);

                    var joined = await collector.WaitForPacketAsync<S_PlayerJoined>(
                        packet => packet.UserId == userId,
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

            public StartedRoomContext(
                long roomId,
                long hostUserId,
                long guestUserId,
                string hostNickname,
                string guestNickname)
            {
                RoomId = roomId;
                HostUserId = hostUserId;
                GuestUserId = guestUserId;
                HostNickname = hostNickname;
                GuestNickname = guestNickname;
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
