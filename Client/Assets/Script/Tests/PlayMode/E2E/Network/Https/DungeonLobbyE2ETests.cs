using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameServer.Grpc.DungeonLobby;
using Grpc.Core;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode.E2E
{
    [TestFixture]
    public class DungeonLobbyE2ETests : E2ETestBase
    {
        [UnityTest]
        public IEnumerator CreateRoom_방_생성_성공() => UniTask.ToCoroutine(async () =>
        {
            await RegisterAndLoginAsync(UniqueEmail(), "Test1234!");

            var response = await LobbyService.CreateRoomAsync(new CreateRoomRequest
            {
                RoomName = "E2E TestRoom",
                MaxPlayers = 4
            }, Timeout());

            Assert.IsTrue(response.Result.Success, response.Result.Message);
            Assert.IsNotNull(response.RoomInfo);
            Assert.AreEqual("E2E TestRoom", response.RoomInfo.RoomName);
            Assert.AreEqual(4, response.RoomInfo.MaxPlayers);
        });

        [UnityTest]
        public IEnumerator GetRoom_생성한_방_조회_성공() => UniTask.ToCoroutine(async () =>
        {
            await RegisterAndLoginAsync(UniqueEmail(), "Test1234!");

            var created = await LobbyService.CreateRoomAsync(new CreateRoomRequest
            {
                RoomName = "E2E GetRoom",
                MaxPlayers = 2
            }, Timeout());

            var response = await LobbyService.GetRoomAsync(new GetRoomRequest
            {
                RoomId = created.RoomInfo.RoomId
            }, Timeout());

            Assert.IsTrue(response.Result.Success, response.Result.Message);
            Assert.AreEqual(created.RoomInfo.RoomId, response.RoomInfo.RoomId);
            Assert.AreEqual("E2E GetRoom", response.RoomInfo.RoomName);
        });

        [UnityTest]
        public IEnumerator GetRoom_존재하지_않는_방_실패() => UniTask.ToCoroutine(async () =>
        {
            await RegisterAndLoginAsync(UniqueEmail(), "Test1234!");

            var response = await LobbyService.GetRoomAsync(new GetRoomRequest
            {
                RoomId = 999999
            }, Timeout());

            Assert.IsFalse(response.Result.Success);
        });

        [UnityTest]
        public IEnumerator GetRooms_방_목록_조회_성공() => UniTask.ToCoroutine(async () =>
        {
            await RegisterAndLoginAsync(UniqueEmail(), "Test1234!");

            await LobbyService.CreateRoomAsync(new CreateRoomRequest
            {
                RoomName = "E2E Room A",
                MaxPlayers = 4
            }, Timeout());

            var response = await LobbyService.GetRoomsAsync(new GetRoomsRequest
            {
                RoomCount = 10
            }, Timeout());

            Assert.IsTrue(response.Result.Success, response.Result.Message);
            Assert.GreaterOrEqual(response.RoomInfos.Count, 1);
        });

        [UnityTest]
        public IEnumerator JoinRoom_다른_유저_입장_성공() => UniTask.ToCoroutine(async () =>
        {
            await RegisterAndLoginAsync(UniqueEmail(), "Test1234!");
            var created = await LobbyService.CreateRoomAsync(new CreateRoomRequest
            {
                RoomName = "E2E JoinRoom",
                MaxPlayers = 4
            }, Timeout());

            await RegisterAndLoginAsync(UniqueEmail(), "Test1234!");
            var response = await LobbyService.JoinRoomAsync(new JoinRoomRequest
            {
                RoomId = created.RoomInfo.RoomId
            }, Timeout());

            Assert.IsTrue(response.Result.Success, response.Result.Message);
            Assert.AreEqual(created.RoomInfo.RoomId, response.RoomInfo.RoomId);
        });

        [UnityTest]
        public IEnumerator JoinRoom_정원초과_실패() => UniTask.ToCoroutine(async () =>
        {
            await RegisterAndLoginAsync(UniqueEmail(), "Test1234!");
            var created = await LobbyService.CreateRoomAsync(new CreateRoomRequest
            {
                RoomName = "E2E FullRoom",
                MaxPlayers = 2
            }, Timeout());
            var roomId = created.RoomInfo.RoomId;

            await RegisterAndLoginAsync(UniqueEmail(), "Test1234!");
            var joined = await LobbyService.JoinRoomAsync(new JoinRoomRequest
            {
                RoomId = roomId
            }, Timeout());
            Assert.IsTrue(joined.Result.Success, joined.Result.Message);

            await RegisterAndLoginAsync(UniqueEmail(), "Test1234!");
            var full = await LobbyService.JoinRoomAsync(new JoinRoomRequest
            {
                RoomId = roomId
            }, Timeout());

            Assert.IsFalse(full.Result.Success);
        });

        [UnityTest]
        public IEnumerator LeaveRoom_입장_후_퇴장_성공() => UniTask.ToCoroutine(async () =>
        {
            await RegisterAndLoginAsync(UniqueEmail(), "Test1234!");
            var created = await LobbyService.CreateRoomAsync(new CreateRoomRequest
            {
                RoomName = "E2E LeaveRoom",
                MaxPlayers = 4
            }, Timeout());

            await RegisterAndLoginAsync(UniqueEmail(), "Test1234!");
            await LobbyService.JoinRoomAsync(new JoinRoomRequest
            {
                RoomId = created.RoomInfo.RoomId
            }, Timeout());

            var response = await LobbyService.LeaveRoomAsync(new LeaveRoomRequest
            {
                RoomId = created.RoomInfo.RoomId
            }, Timeout());

            Assert.IsTrue(response.Result.Success, response.Result.Message);
        });

        [UnityTest]
        public IEnumerator UpdateRoom_방장만_설정_변경_성공() => UniTask.ToCoroutine(async () =>
        {
            await RegisterAndLoginAsync(UniqueEmail(), "Test1234!");
            var created = await LobbyService.CreateRoomAsync(new CreateRoomRequest
            {
                RoomName = "Original Name",
                MaxPlayers = 2
            }, Timeout());

            var response = await LobbyService.UpdateRoomAsync(new UpdateRoomRequest
            {
                RoomId = created.RoomInfo.RoomId,
                RoomName = "Updated Name",
                MaxPlayers = 4
            }, Timeout());

            Assert.IsTrue(response.Result.Success, response.Result.Message);
            Assert.AreEqual("Updated Name", response.RoomInfo.RoomName);
            Assert.AreEqual(4, response.RoomInfo.MaxPlayers);
        });

        [UnityTest]
        public IEnumerator UpdateRoom_비방장_실패() => UniTask.ToCoroutine(async () =>
        {
            await RegisterAndLoginAsync(UniqueEmail(), "Test1234!");
            var created = await LobbyService.CreateRoomAsync(new CreateRoomRequest
            {
                RoomName = "Host Room",
                MaxPlayers = 2
            }, Timeout());

            await RegisterAndLoginAsync(UniqueEmail(), "Test1234!");
            await LobbyService.JoinRoomAsync(new JoinRoomRequest
            {
                RoomId = created.RoomInfo.RoomId
            }, Timeout());

            var response = await LobbyService.UpdateRoomAsync(new UpdateRoomRequest
            {
                RoomId = created.RoomInfo.RoomId,
                RoomName = "Hacked Name",
                MaxPlayers = 4
            }, Timeout());

            Assert.IsFalse(response.Result.Success);
        });

        [UnityTest]
        public IEnumerator SubscribeRoom_이벤트_수신() => UniTask.ToCoroutine(async () =>
        {
            await RegisterAndLoginAsync(UniqueEmail(), "Test1234!");
            var created = await LobbyService.CreateRoomAsync(new CreateRoomRequest
            {
                RoomName = "E2E Subscribe",
                MaxPlayers = 4
            }, Timeout());
            var roomId = created.RoomInfo.RoomId;

            var received = new List<SubscribeRoomResponse>();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            var subscribeTask = LobbyService.SubscribeRoomAsync(
                new SubscribeRoomRequest { RoomId = roomId },
                msg => received.Add(msg),
                cts.Token).AsTask();

            await RegisterAndLoginAsync(UniqueEmail(), "Test1234!");
            await LobbyService.JoinRoomAsync(new JoinRoomRequest
            {
                RoomId = roomId
            }, Timeout());

            try
            {
                await subscribeTask;
            }
            catch (OperationCanceledException)
            {
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled)
            {
            }

            Assert.GreaterOrEqual(received.Count, 1);
        });

        [UnityTest]
        public IEnumerator StartRoom_비방장_실패() => UniTask.ToCoroutine(async () =>
        {
            var hostEmail = UniqueEmail();

            await RegisterAndLoginAsync(hostEmail, "Test1234!");
            var created = await LobbyService.CreateRoomAsync(new CreateRoomRequest
            {
                RoomName = "E2E Start Fail",
                MaxPlayers = 2
            }, Timeout());
            var roomId = created.RoomInfo.RoomId;

            await RegisterAndLoginAsync(UniqueEmail(), "Test1234!");
            await LobbyService.JoinRoomAsync(new JoinRoomRequest
            {
                RoomId = roomId
            }, Timeout());

            var response = await LobbyService.StartRoomAsync(new StartRoomRequest
            {
                RoomId = roomId
            }, Timeout());

            Assert.IsFalse(response.Result.Success);
        });

        [UnityTest]
        public IEnumerator 전체흐름_방생성_입장_시작() => UniTask.ToCoroutine(async () =>
        {
            var hostEmail = UniqueEmail();

            await RegisterAndLoginAsync(hostEmail, "Test1234!");
            var created = await LobbyService.CreateRoomAsync(new CreateRoomRequest
            {
                RoomName = "E2E Full Flow",
                MaxPlayers = 2
            }, Timeout());
            var roomId = created.RoomInfo.RoomId;
            Assert.IsTrue(created.Result.Success);

            await RegisterAndLoginAsync(UniqueEmail(), "Test1234!");
            var joined = await LobbyService.JoinRoomAsync(new JoinRoomRequest
            {
                RoomId = roomId
            }, Timeout());
            Assert.IsTrue(joined.Result.Success);

            await LoginAsync(hostEmail, "Test1234!");
            var started = await LobbyService.StartRoomAsync(new StartRoomRequest
            {
                RoomId = roomId
            }, Timeout());

            Assert.IsTrue(started.Result.Success, started.Result.Message);
        });

        /// <summary>
        /// 로그 재현 시나리오:
        ///   방 생성 → SubscribeRoom 구독 시작 → StartGame 요청
        ///   → UpdateEvent(Starting) 수신 → GameSessionEvent(SocketIp/Port) 수신
        ///
        /// OutboxPublisher 1초 폴링 + SocketServer 처리 + GameSessionReadyConsumer 포함.
        /// 전체 체인이 끊기면 타임아웃으로 실패한다.
        /// </summary>
        [UnityTest]
        public IEnumerator StartRoom_구독중_GameSessionEvent_수신() => UniTask.ToCoroutine(async () =>
        {
            // Arrange: 방 생성 (호스트 단독 — 로그 시나리오 그대로)
            await RegisterAndLoginAsync(UniqueEmail(), "Test1234!");
            var created = await LobbyService.CreateRoomAsync(new CreateRoomRequest
            {
                RoomName = "E2E StartGame Flow",
                MaxPlayers = 4
            }, Timeout());
            Assert.IsTrue(created.Result.Success, created.Result.Message);
            var roomId = created.RoomInfo.RoomId;

            // SubscribeRoom 구독 시작 (StartGame 전에 — 앱과 동일한 순서)
            var received = new List<SubscribeRoomResponse>();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));

            var subscribeTask = LobbyService.SubscribeRoomAsync(
                new SubscribeRoomRequest { RoomId = roomId },
                msg =>
                {
                    received.Add(msg);
                    if (msg.PayloadCase == SubscribeRoomResponse.PayloadOneofCase.GameSessionEvent)
                        cts.Cancel(); // GameSessionEvent 수신 시 구독 종료
                },
                cts.Token).AsTask();

            // Act: StartGame 요청
            var started = await LobbyService.StartRoomAsync(
                new StartRoomRequest { RoomId = roomId }, Timeout());
            Assert.IsTrue(started.Result.Success, started.Result.Message);

            // GameSessionEvent 대기
            try { await subscribeTask; }
            catch (OperationCanceledException) { }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled) { }

            // Assert: UpdateEvent(Starting) 수신 확인
            Assert.IsTrue(
                received.Exists(r => r.PayloadCase == SubscribeRoomResponse.PayloadOneofCase.UpdateEvent),
                "StartGame 후 UpdateEvent(Starting)를 수신해야 한다");

            // Assert: GameSessionEvent 수신 확인
            var gameSessionEvent = received.Find(
                r => r.PayloadCase == SubscribeRoomResponse.PayloadOneofCase.GameSessionEvent);
            Assert.IsNotNull(gameSessionEvent,
                $"GameSessionEvent 미수신 — 받은 이벤트: [{string.Join(", ", received.ConvertAll(r => r.PayloadCase.ToString()))}]");

            Assert.IsFalse(string.IsNullOrEmpty(gameSessionEvent.GameSessionEvent.Ip),
                "GameSessionEvent.Ip가 있어야 한다");
            Assert.Greater(gameSessionEvent.GameSessionEvent.Port, 0,
                "GameSessionEvent.Port가 0보다 커야 한다");
        });
    }
}
