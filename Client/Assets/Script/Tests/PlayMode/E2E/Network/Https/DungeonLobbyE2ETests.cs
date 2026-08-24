using System;
using System.Linq;
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
        public IEnumerator GetRooms_요청한_페이지_크기를_서버가_지킨다() => UniTask.ToCoroutine(async () =>
        {
            // 9.6 회귀 고정: 예전 서버는 room_count 를 **완전히 무시**하고 전체 활성 방을 반환했다.
            // 방이 2개 이상인 상태에서 1개만 요청해 그 계약을 실서버로 확인한다.
            await RegisterAndLoginAsync(UniqueEmail(), "Test1234!");
            await LobbyService.CreateRoomAsync(new CreateRoomRequest
            {
                RoomName = "E2E Paging A", MaxPlayers = 4
            }, Timeout());

            // 한 유저는 한 방만 가질 수 있어 두 번째 방은 다른 계정으로 만든다.
            await RegisterAndLoginAsync(UniqueEmail(), "Test1234!");
            await LobbyService.CreateRoomAsync(new CreateRoomRequest
            {
                RoomName = "E2E Paging B", MaxPlayers = 4
            }, Timeout());

            var page = await LobbyService.GetRoomsAsync(new GetRoomsRequest
            {
                RoomCount = 1, Offset = 0
            }, Timeout());

            Assert.IsTrue(page.Result.Success, page.Result.Message);
            Assert.AreEqual(1, page.RoomInfos.Count, "서버가 요청한 페이지 크기를 지켜야 한다");
            Assert.GreaterOrEqual(page.TotalCount, 2, "전체 활성 방 수는 페이지 크기와 무관하게 알려줘야 한다");

            // 정렬·중복 검증은 **한 응답 안에서만** 한다.
            // 두 번의 RPC 로 offset 0/1 을 비교하면, 그 사이 다른 클라이언트가 방을 만드는 순간
            // 앞쪽 삽입으로 같은 방이 두 페이지에 나온다 — 그건 offset 페이징의 정상 동작이라 경합 테스트가 된다.
            // (실측: 4290·4291 생성 → offset0=4291 → 다른 세션이 4292 생성 → offset1=4291)
            // offset/limit 산술 자체는 서버 단위 테스트가 결정적으로 고정한다
            // (DungeonLobbyServiceTests.GetActiveDungeonRooms_페이지들이_겹치지_않고_전체를_덮는다).
            var twoAtOnce = await LobbyService.GetRoomsAsync(new GetRoomsRequest
            {
                RoomCount = 2, Offset = 0
            }, Timeout());

            Assert.IsTrue(twoAtOnce.Result.Success, twoAtOnce.Result.Message);
            Assert.AreEqual(2, twoAtOnce.RoomInfos.Count, "서버가 요청한 페이지 크기를 지켜야 한다");
            Assert.AreNotEqual(twoAtOnce.RoomInfos[0].RoomId, twoAtOnce.RoomInfos[1].RoomId,
                "한 페이지 안에 같은 방이 두 번 오면 안 된다");
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

            // 호스트를 뺀 전원이 준비해야 StartRoom 이 통과한다(준비 게이트, 서버 권위).
            var guestReady = await LobbyService.SetReadyAsync(new SetReadyRequest
            {
                RoomId  = roomId,
                IsReady = true
            }, Timeout());
            Assert.IsTrue(guestReady.Result.Success, guestReady.Result.Message);

            await LoginAsync(hostEmail, "Test1234!");
            var started = await LobbyService.StartRoomAsync(new StartRoomRequest
            {
                RoomId = roomId
            }, Timeout());

            Assert.IsTrue(started.Result.Success, started.Result.Message);
        });

        /// <summary>
        /// 준비 게이트(서버 권위): 비호스트가 준비하지 않으면 호스트가 시작을 눌러도 거부된다.
        /// 클라 버튼 비활성화는 UX 일 뿐이라 RPC 를 직접 쏴도 막혀야 한다.
        /// </summary>
        [UnityTest]
        public IEnumerator StartRoom_비호스트_미준비면_거부된다() => UniTask.ToCoroutine(async () =>
        {
            var hostEmail = UniqueEmail();

            await RegisterAndLoginAsync(hostEmail, "Test1234!");
            var created = await LobbyService.CreateRoomAsync(new CreateRoomRequest
            {
                RoomName   = "E2E Ready Gate",
                MaxPlayers = 2
            }, Timeout());
            Assert.IsTrue(created.Result.Success, created.Result.Message);
            var roomId = created.RoomInfo.RoomId;

            await RegisterAndLoginAsync(UniqueEmail(), "Test1234!");
            var joined = await LobbyService.JoinRoomAsync(new JoinRoomRequest { RoomId = roomId }, Timeout());
            Assert.IsTrue(joined.Result.Success, joined.Result.Message);

            // 준비하지 않은 채로 호스트가 시작 시도
            await LoginAsync(hostEmail, "Test1234!");
            var started = await LobbyService.StartRoomAsync(new StartRoomRequest { RoomId = roomId }, Timeout());

            Assert.IsFalse(started.Result.Success, "미준비 인원이 있으면 시작이 거부돼야 한다");

            // 준비 후에는 통과해야 한다 — 게이트가 영구 차단이 아님을 함께 확인한다.
            var room = await LobbyService.GetRoomAsync(new GetRoomRequest { RoomId = roomId }, Timeout());
            Assert.AreEqual(RoomStatusType.Waiting, room.RoomInfo.Status, "거부된 시작은 방 상태를 바꾸지 않아야 한다");
        });

        /// <summary>
        /// SetReady 결과가 RoomInfo.ready_public_ids 로 노출되고, 호스트는 그 목록에 담기지 않는다.
        /// </summary>
        [UnityTest]
        public IEnumerator SetReady_준비하면_RoomInfo에_반영되고_호스트는_제외된다() => UniTask.ToCoroutine(async () =>
        {
            var hostEmail = UniqueEmail();

            await RegisterAndLoginAsync(hostEmail, "Test1234!");
            var created = await LobbyService.CreateRoomAsync(new CreateRoomRequest
            {
                RoomName   = "E2E Ready Flag",
                MaxPlayers = 2
            }, Timeout());
            var roomId       = created.RoomInfo.RoomId;
            var hostPublicId = created.RoomInfo.HostPublicId;
            Assert.IsFalse(string.IsNullOrEmpty(hostPublicId), "RoomInfo 는 방장의 public_id 를 실어야 한다");

            await RegisterAndLoginAsync(UniqueEmail(), "Test1234!");
            await LobbyService.JoinRoomAsync(new JoinRoomRequest { RoomId = roomId }, Timeout());

            var ready = await LobbyService.SetReadyAsync(new SetReadyRequest
            {
                RoomId  = roomId,
                IsReady = true
            }, Timeout());
            Assert.IsTrue(ready.Result.Success, ready.Result.Message);
            Assert.AreEqual(1, ready.RoomInfo.ReadyPublicIds.Count);
            Assert.IsFalse(ready.RoomInfo.ReadyPublicIds.Contains(hostPublicId), "호스트는 준비 목록에 담기지 않는다");

            // 해제하면 목록에서 빠진다
            var unready = await LobbyService.SetReadyAsync(new SetReadyRequest
            {
                RoomId  = roomId,
                IsReady = false
            }, Timeout());
            Assert.IsTrue(unready.Result.Success, unready.Result.Message);
            Assert.AreEqual(0, unready.RoomInfo.ReadyPublicIds.Count);
        });

        /// <summary>
        /// 늦게 들어온 사람이 <b>입장 응답에서 바로</b> 기존 인원의 준비 상태를 볼 수 있어야 한다.
        /// 여기가 비면 대기실을 열자마자는 전원 미준비로 보이고, 누군가 버튼을 눌러
        /// UpdateEvent 가 올 때까지 화면이 틀린 상태로 남는다.
        /// </summary>
        [UnityTest]
        public IEnumerator JoinRoom_이미_준비한_사람이_있으면_입장_응답에_그_준비가_실려온다() => UniTask.ToCoroutine(async () =>
        {
            var hostEmail = UniqueEmail();
            await RegisterAndLoginAsync(hostEmail, "Test1234!");
            var created = await LobbyService.CreateRoomAsync(new CreateRoomRequest
            {
                RoomName   = "E2E Late Joiner",
                MaxPlayers = 4
            }, Timeout());
            var roomId = created.RoomInfo.RoomId;

            // 먼저 들어온 게스트가 준비를 누른다.
            await RegisterAndLoginAsync(UniqueEmail(), "Test1234!");
            await LobbyService.JoinRoomAsync(new JoinRoomRequest { RoomId = roomId }, Timeout());
            var ready = await LobbyService.SetReadyAsync(new SetReadyRequest
            {
                RoomId  = roomId,
                IsReady = true
            }, Timeout());
            Assert.IsTrue(ready.Result.Success, ready.Result.Message);
            Assert.AreEqual(1, ready.RoomInfo.ReadyPublicIds.Count);
            var readyPublicId = ready.RoomInfo.ReadyPublicIds[0];

            // 늦게 들어온 사람의 입장 응답
            await RegisterAndLoginAsync(UniqueEmail(), "Test1234!");
            var joined = await LobbyService.JoinRoomAsync(new JoinRoomRequest { RoomId = roomId }, Timeout());

            Assert.IsTrue(joined.Result.Success, joined.Result.Message);
            Assert.IsFalse(string.IsNullOrEmpty(joined.RoomInfo.HostPublicId), "입장 응답에 방장 식별자가 있어야 한다");
            Assert.Contains(readyPublicId, joined.RoomInfo.ReadyPublicIds,
                "입장 응답이 기존 인원의 준비 상태를 실어야 대기실이 열리자마자 올바르게 그려진다");
        });

        /// <summary>
        /// 방 <b>목록</b>(GetRooms)도 준비 상태를 실어야 한다.
        /// 목록에서 방을 고르면 그 스냅샷이 그대로 대기실 State(SelectedRoom)로 들어가기 때문이다.
        /// </summary>
        [UnityTest]
        public IEnumerator GetRooms_목록에도_준비_상태와_방장이_실려온다() => UniTask.ToCoroutine(async () =>
        {
            await RegisterAndLoginAsync(UniqueEmail(), "Test1234!");
            var created = await LobbyService.CreateRoomAsync(new CreateRoomRequest
            {
                RoomName   = "E2E List Ready",
                MaxPlayers = 4
            }, Timeout());
            var roomId = created.RoomInfo.RoomId;

            await RegisterAndLoginAsync(UniqueEmail(), "Test1234!");
            await LobbyService.JoinRoomAsync(new JoinRoomRequest { RoomId = roomId }, Timeout());
            var ready = await LobbyService.SetReadyAsync(new SetReadyRequest
            {
                RoomId  = roomId,
                IsReady = true
            }, Timeout());
            var readyPublicId = ready.RoomInfo.ReadyPublicIds[0];

            var rooms = await LobbyService.GetRoomsAsync(new GetRoomsRequest { RoomCount = 50, Offset = 0 }, Timeout());
            var mine = rooms.RoomInfos.FirstOrDefault(r => r.RoomId == roomId);

            Assert.IsNotNull(mine, "방금 만든 방이 목록에 있어야 한다");
            Assert.IsFalse(string.IsNullOrEmpty(mine.HostPublicId), "목록에도 방장 식별자가 있어야 한다");
            Assert.Contains(readyPublicId, mine.ReadyPublicIds,
                "목록에도 준비 상태가 실려야 목록→대기실 전환 시 화면이 틀리지 않는다");
        });

        /// <summary>
        /// 호스트는 준비 개념이 없어 SetReady 가 거부된다.
        /// </summary>
        [UnityTest]
        public IEnumerator SetReady_호스트는_거부된다() => UniTask.ToCoroutine(async () =>
        {
            await RegisterAndLoginAsync(UniqueEmail(), "Test1234!");
            var created = await LobbyService.CreateRoomAsync(new CreateRoomRequest
            {
                RoomName   = "E2E Host Ready",
                MaxPlayers = 2
            }, Timeout());

            var result = await LobbyService.SetReadyAsync(new SetReadyRequest
            {
                RoomId  = created.RoomInfo.RoomId,
                IsReady = true
            }, Timeout());

            Assert.IsFalse(result.Result.Success, "호스트의 준비 토글은 거부돼야 한다");
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
