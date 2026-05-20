using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Game.Network.Https.Services;
using Game.Tests.EditMode.Network.Fakes;
using GameServer.Grpc.Common;
using GameServer.Grpc.DungeonLobby;
using Grpc.Core;
using NUnit.Framework;

namespace Game.Tests.EditMode.Network
{
    [TestFixture]
    public class DungeonLobbyGrpcServiceTests
    {
        private FakeCallInvoker _invoker;
        private DungeonLobbyGrpcService _service;

        private static readonly RoomInfo SampleRoom = new RoomInfo
        {
            RoomId = 1001,
            RoomName = "TestRoom",
            MaxPlayers = 4
        };

        [SetUp]
        public void SetUp()
        {
            _invoker = new FakeCallInvoker();
            _service = new DungeonLobbyGrpcService(new FakeGrpcChannelProvider(_invoker));
        }

        // ─── CreateRoomAsync ─────────────────────────────────────────

        [Test]
        public async Task CreateRoomAsync_성공응답_방_정보_반환()
        {
            _invoker.SetResponse("CreateRoom", new CreateRoomResponse
            {
                Result = new Result { Success = true },
                RoomInfo = SampleRoom
            });

            var response = await _service.CreateRoomAsync(new CreateRoomRequest
            {
                RoomName = "TestRoom",
                MaxPlayers = 4
            }).AsTask();

            Assert.IsNotNull(response);
            Assert.IsTrue(response.Result.Success);
            Assert.AreEqual(SampleRoom.RoomId, response.RoomInfo.RoomId);
            Assert.AreEqual(SampleRoom.RoomName, response.RoomInfo.RoomName);
        }

        [Test]
        public async Task CreateRoomAsync_서버_오류_RpcException_발생()
        {
            _invoker.SetError("CreateRoom", StatusCode.Internal, "Server error.");

            var ex = await Throws<RpcException>(() =>
                _service.CreateRoomAsync(new CreateRoomRequest()).AsTask());

            Assert.AreEqual(StatusCode.Internal, ex.StatusCode);
        }

        // ─── GetRoomAsync ────────────────────────────────────────────

        [Test]
        public async Task GetRoomAsync_성공응답_방_정보_반환()
        {
            _invoker.SetResponse("GetRoom", new GetRoomResponse
            {
                Result = new Result { Success = true },
                RoomInfo = SampleRoom
            });

            var response = await _service.GetRoomAsync(new GetRoomRequest { RoomId = 1001 }).AsTask();

            Assert.IsNotNull(response);
            Assert.IsTrue(response.Result.Success);
            Assert.AreEqual(1001, response.RoomInfo.RoomId);
        }

        [Test]
        public async Task GetRoomAsync_존재하지_않는_방_RpcException_발생()
        {
            _invoker.SetError("GetRoom", StatusCode.NotFound, "Room not found.");

            var ex = await Throws<RpcException>(() =>
                _service.GetRoomAsync(new GetRoomRequest { RoomId = 9999 }).AsTask());

            Assert.AreEqual(StatusCode.NotFound, ex.StatusCode);
        }

        // ─── GetRoomsAsync ───────────────────────────────────────────

        [Test]
        public async Task GetRoomsAsync_성공응답_방_목록_반환()
        {
            var room2 = new RoomInfo { RoomId = 1002, RoomName = "Room2", MaxPlayers = 2 };
            var response = new GetRoomsResponse { Result = new Result { Success = true } };
            response.RoomInfos.Add(SampleRoom);
            response.RoomInfos.Add(room2);

            _invoker.SetResponse("GetRooms", response);

            var result = await _service.GetRoomsAsync(new GetRoomsRequest { RoomCount = 10 }).AsTask();

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Result.Success);
            Assert.AreEqual(2, result.RoomInfos.Count);
        }

        [Test]
        public async Task GetRoomsAsync_빈_목록_반환()
        {
            _invoker.SetResponse("GetRooms", new GetRoomsResponse
            {
                Result = new Result { Success = true }
            });

            var result = await _service.GetRoomsAsync(new GetRoomsRequest { RoomCount = 10 }).AsTask();

            Assert.AreEqual(0, result.RoomInfos.Count);
        }

        [Test]
        public async Task GetRoomsAsync_서버_오류_RpcException_발생()
        {
            _invoker.SetError("GetRooms", StatusCode.Internal, "Server error.");

            var ex = await Throws<RpcException>(() =>
                _service.GetRoomsAsync(new GetRoomsRequest { RoomCount = 10 }).AsTask());

            Assert.AreEqual(StatusCode.Internal, ex.StatusCode);
        }

        // ─── JoinRoomAsync ───────────────────────────────────────────

        [Test]
        public async Task JoinRoomAsync_성공응답_반환()
        {
            _invoker.SetResponse("JoinRoom", new JoinRoomResponse
            {
                Result = new Result { Success = true },
                RoomInfo = SampleRoom
            });

            var response = await _service.JoinRoomAsync(new JoinRoomRequest { RoomId = 1001 }).AsTask();

            Assert.IsNotNull(response);
            Assert.IsTrue(response.Result.Success);
            Assert.AreEqual(1001, response.RoomInfo.RoomId);
        }

        [Test]
        public async Task JoinRoomAsync_방이_가득_찬_경우_RpcException_발생()
        {
            _invoker.SetError("JoinRoom", StatusCode.ResourceExhausted, "Room is full.");

            var ex = await Throws<RpcException>(() =>
                _service.JoinRoomAsync(new JoinRoomRequest { RoomId = 1001 }).AsTask());

            Assert.AreEqual(StatusCode.ResourceExhausted, ex.StatusCode);
        }

        // ─── LeaveRoomAsync ──────────────────────────────────────────

        [Test]
        public async Task LeaveRoomAsync_성공응답_반환()
        {
            _invoker.SetResponse("LeaveRoom", new LeaveRoomResponse
            {
                Result = new Result { Success = true }
            });

            var response = await _service.LeaveRoomAsync(new LeaveRoomRequest { RoomId = 1001 }).AsTask();

            Assert.IsNotNull(response);
            Assert.IsTrue(response.Result.Success);
        }

        [Test]
        public async Task LeaveRoomAsync_없는_방_RpcException_발생()
        {
            _invoker.SetError("LeaveRoom", StatusCode.NotFound, "Room not found.");

            var ex = await Throws<RpcException>(() =>
                _service.LeaveRoomAsync(new LeaveRoomRequest { RoomId = 9999 }).AsTask());

            Assert.AreEqual(StatusCode.NotFound, ex.StatusCode);
        }

        // ─── UpdateRoomAsync ─────────────────────────────────────────

        [Test]
        public async Task UpdateRoomAsync_성공응답_반환()
        {
            _invoker.SetResponse("UpdateRoom", new UpdateRoomResponse
            {
                Result = new Result { Success = true },
                RoomInfo = new RoomInfo { RoomId = 1001, RoomName = "UpdatedRoom", MaxPlayers = 4 }
            });

            var response = await _service.UpdateRoomAsync(new UpdateRoomRequest
            {
                RoomId = 1001,
                RoomName = "UpdatedRoom",
                MaxPlayers = 4
            }).AsTask();

            Assert.IsNotNull(response);
            Assert.IsTrue(response.Result.Success);
            Assert.AreEqual("UpdatedRoom", response.RoomInfo.RoomName);
        }

        [Test]
        public async Task UpdateRoomAsync_권한_없음_RpcException_발생()
        {
            _invoker.SetError("UpdateRoom", StatusCode.PermissionDenied, "Only host can update.");

            var ex = await Throws<RpcException>(() =>
                _service.UpdateRoomAsync(new UpdateRoomRequest { RoomId = 1001 }).AsTask());

            Assert.AreEqual(StatusCode.PermissionDenied, ex.StatusCode);
        }

        // ─── StartRoomAsync ──────────────────────────────────────────

        [Test]
        public async Task StartRoomAsync_성공응답_반환()
        {
            _invoker.SetResponse("StartRoom", new StartRoomResponse
            {
                Result = new Result { Success = true },
                RoomInfo = SampleRoom
            });

            var response = await _service.StartRoomAsync(new StartRoomRequest { RoomId = 1001 }).AsTask();

            Assert.IsNotNull(response);
            Assert.IsTrue(response.Result.Success);
        }

        [Test]
        public async Task StartRoomAsync_플레이어_부족_RpcException_발생()
        {
            _invoker.SetError("StartRoom", StatusCode.FailedPrecondition, "Not enough players.");

            var ex = await Throws<RpcException>(() =>
                _service.StartRoomAsync(new StartRoomRequest { RoomId = 1001 }).AsTask());

            Assert.AreEqual(StatusCode.FailedPrecondition, ex.StatusCode);
        }

        // ─── SubscribeRoomAsync (ServerStreaming) ────────────────────

        [Test]
        public async Task SubscribeRoomAsync_메시지_수신됨()
        {
            var msg1 = new SubscribeRoomResponse
            {
                UpdateEvent = new RoomUpdatedEvent { RoomInfo = SampleRoom }
            };
            var msg2 = new SubscribeRoomResponse
            {
                StartEvent = new GameStartedEvent { RoomInfo = SampleRoom }
            };
            _invoker.SetStreamResponses("SubscribeRoom", new[] { msg1, msg2 });

            var received = new List<SubscribeRoomResponse>();
            await _service.SubscribeRoomAsync(
                new SubscribeRoomRequest { RoomId = 1001 },
                msg => received.Add(msg)).AsTask();

            Assert.AreEqual(2, received.Count);
            Assert.AreEqual(SubscribeRoomResponse.PayloadOneofCase.UpdateEvent, received[0].PayloadCase);
            Assert.AreEqual(SubscribeRoomResponse.PayloadOneofCase.StartEvent, received[1].PayloadCase);
        }

        [Test]
        public async Task SubscribeRoomAsync_빈_스트림_콜백_호출_안됨()
        {
            _invoker.SetStreamResponses("SubscribeRoom", new SubscribeRoomResponse[0]);

            var callCount = 0;
            await _service.SubscribeRoomAsync(
                new SubscribeRoomRequest { RoomId = 1001 },
                _ => callCount++).AsTask();

            Assert.AreEqual(0, callCount);
        }

        [Test]
        public async Task SubscribeRoomAsync_메시지_순서_보장됨()
        {
            var messages = new[]
            {
                new SubscribeRoomResponse { UpdateEvent = new RoomUpdatedEvent { RoomInfo = SampleRoom } },
                new SubscribeRoomResponse { StartEvent  = new GameStartedEvent { RoomInfo = SampleRoom } },
            };
            _invoker.SetStreamResponses("SubscribeRoom", messages);

            var received = new List<SubscribeRoomResponse.PayloadOneofCase>();
            await _service.SubscribeRoomAsync(
                new SubscribeRoomRequest { RoomId = 1001 },
                msg => received.Add(msg.PayloadCase)).AsTask();

            Assert.AreEqual(SubscribeRoomResponse.PayloadOneofCase.UpdateEvent, received[0]);
            Assert.AreEqual(SubscribeRoomResponse.PayloadOneofCase.StartEvent,  received[1]);
        }

        [Test]
        public async Task SubscribeRoomAsync_스트림_오류_RpcException_발생()
        {
            _invoker.SetError("SubscribeRoom", StatusCode.Unavailable, "Stream disconnected.");

            var ex = await Throws<RpcException>(() =>
                _service.SubscribeRoomAsync(
                    new SubscribeRoomRequest { RoomId = 1001 },
                    _ => { }).AsTask());

            Assert.AreEqual(StatusCode.Unavailable, ex.StatusCode);
        }

        [Test]
        public async Task SubscribeRoomAsync_취소토큰_이미_취소된_경우_예외_발생()
        {
            _invoker.SetStreamResponses("SubscribeRoom", new[] { new SubscribeRoomResponse() });

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            Assert.ThrowsAsync<TaskCanceledException>(async () =>
                await _service.SubscribeRoomAsync(
                    new SubscribeRoomRequest { RoomId = 1001 },
                    _ => { },
                    cts.Token).AsTask());
        }

        // ─── 헬퍼 ───────────────────────────────────────────────────

        private static async Task<T> Throws<T>(Func<Task> action) where T : Exception
        {
            try
            {
                await action();
                Assert.Fail($"{typeof(T).Name} 이 발생해야 했지만 발생하지 않았다.");
                return null;
            }
            catch (T ex)
            {
                return ex;
            }
        }
    }
}
