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
    }
}
