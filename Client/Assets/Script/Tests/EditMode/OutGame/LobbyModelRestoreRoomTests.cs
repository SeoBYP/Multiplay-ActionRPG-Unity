using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Presentation.DungeonLobby;
using Game.System.DungeonLobby;
using GameServer.Grpc.DungeonLobby;
using NUnit.Framework;
using R3;
using Game.System.Auth;
using Script.System.Startup;

namespace Game.Tests.EditMode.OutGame
{
    /// <summary>
    /// LobbyModel.RestoreRoom 의 "이미 종료된 방" 분기 검증.
    ///
    /// 재로그인 시 세션에 저장된 roomId로 방을 복원하는데,
    /// 그 사이 방이 Closed 됐다면 RoomDetail을 열어선 안 된다.
    /// (NavigateToRoom 미발행 + ErrorMessage 설정)
    /// </summary>
    [TestFixture]
    public class LobbyModelRestoreRoomTests
    {
        private sealed class FakeDungeonLobbyService : IDungeonLobbyService
        {
            public bool IsInRoom { get; set; }
            public RoomInfo CurrentRoom { get; set; }

            public event Action<RoomInfo> OnRoomUpdated;
            public event Action<RoomInfo> OnGameStarting;
            public event Action<string, int, long> OnGameSessionReady;

            // 미사용 이벤트 경고 억제용 (테스트에서 직접 발생시키지 않음)
            public void RaiseAll()
            {
                OnRoomUpdated?.Invoke(CurrentRoom);
                OnGameStarting?.Invoke(CurrentRoom);
                OnGameSessionReady?.Invoke("", 0, 0);
            }

            public UniTask<(DungeonLobbyResult, IReadOnlyList<RoomInfo>, int TotalCount)> GetRoomsAsync(
                int offset = 0, int limit = DungeonLobbyPaging.DefaultPageSize, CancellationToken ct = default)
                => UniTask.FromResult((DungeonLobbyResult.Success, (IReadOnlyList<RoomInfo>)new List<RoomInfo>(), 0));

            /// <summary>마지막 CreateRoom 호출에 전달된 mapId(던전 선택 전파 검증용).</summary>
            public string LastCreateMapId { get; private set; }

            public UniTask<DungeonLobbyResult> CreateRoomAsync(string roomName, int maxPlayers, string mapId = "", CancellationToken ct = default)
            {
                LastCreateMapId = mapId;
                return UniTask.FromResult(DungeonLobbyResult.Success);
            }

            public UniTask<DungeonLobbyResult> JoinRoomAsync(long roomId, CancellationToken ct = default)
                => UniTask.FromResult(DungeonLobbyResult.Success);

            public UniTask<DungeonLobbyResult> LeaveRoomAsync(CancellationToken ct = default)
                => UniTask.FromResult(DungeonLobbyResult.Success);

            public UniTask<DungeonLobbyResult> StartGameAsync(CancellationToken ct = default)
                => UniTask.FromResult(DungeonLobbyResult.Success);

            public UniTask<DungeonLobbyResult> RestoreRoomAsync(long roomId, CancellationToken ct = default)
                => UniTask.FromResult(DungeonLobbyResult.Success);

            /// <summary>마지막으로 요청된 준비 상태. null = 호출된 적 없음.</summary>
            public bool? LastSetReady { get; private set; }

            public UniTask<DungeonLobbyResult> SetReadyAsync(bool isReady, CancellationToken ct = default)
            {
                LastSetReady = isReady;
                return UniTask.FromResult(DungeonLobbyResult.Success);
            }
        }

        private sealed class FakeAuthService : IAuthService
        {
            public bool IsAuthenticated => true;
            public UniTask AuthenticatedAsync() => UniTask.CompletedTask;
            public UniTask<AuthResult> TryAutoLoginAsync(CancellationToken ct) => UniTask.FromResult(AuthResult.Success);
            public UniTask<AuthResult> LoginOrRegisterAsync(string email, string password, CancellationToken ct) => UniTask.FromResult(AuthResult.Success);
            public void Logout() { }
            public UniTask<AuthResult> RefreshTokenAsync(CancellationToken ct) => UniTask.FromResult(AuthResult.Success);
        }

        private sealed class FakeInputContext : Game.System.Input.IInputContext
        {
            public bool IsUiActive { get; private set; }
            public void EnterUi() => IsUiActive = true;
            public void ExitUi()  => IsUiActive = false;
        }

        private static LobbyModel BuildModel(FakeDungeonLobbyService service)
        {
            var repository = new LobbyRepository(service);
            return new LobbyModel(repository, service, new FakeAuthService(), new UserProfile(), new StartupIntentQueue(), new FakeInputContext());
        }

        /// <summary>
        /// 대기실 View 는 방 입장이 <b>끝난 뒤</b> Addressable 로 로드되므로, State 가 이미 확정된 상태에서
        /// 늦게 구독한다. 이때 구독 즉시 현재 State 를 받아야 "열자마자" UI 가 그려진다.
        /// 못 받으면 다음 State 변경(= 버튼을 눌러야 발생)까지 화면이 비어 있다.
        /// </summary>
        [Test]
        public void 늦게_구독해도_현재_State를_즉시_받는다()
        {
            var service = new FakeDungeonLobbyService
            {
                CurrentRoom = new RoomInfo
                {
                    RoomId = 7, RoomName = "late-subscribe", MaxPlayers = 4,
                    Status = RoomStatusType.Waiting, HostPublicId = "host"
                }
            };
            var model = BuildModel(service);

            // 방 입장 완료 — View 가 열리기 전에 State 가 이미 확정된다.
            model.Accept(new LobbyIntent.JoinRoom(7));

            // View 가 이제서야 로드돼 구독한다.
            LobbyState received = null;
            using var _ = model.State.Subscribe(s => received = s);

            Assert.IsNotNull(received, "구독 즉시 현재 State 를 받아야 한다");
            Assert.IsNotNull(received.SelectedRoom, "SelectedRoom 이 실려 있어야 대기실이 그려진다");
            Assert.AreEqual(7, received.SelectedRoom.RoomId);
        }

        [Test]
        public void RestoreRoom_방이_Closed면_NavigateToRoom을_발행하지_않는다()
        {
            var service = new FakeDungeonLobbyService
            {
                CurrentRoom = new RoomInfo
                {
                    RoomId = 1,
                    RoomName = "dead-room",
                    MaxPlayers = 4,
                    Status = RoomStatusType.Closed
                }
            };
            var model = BuildModel(service);

            var navigated = false;
            using var _ = model.NavigateToRoom.Subscribe(__ => navigated = true);

            model.Accept(new LobbyIntent.RestoreRoom(1));

            Assert.IsFalse(navigated, "Closed 방은 RoomDetail로 네비게이트되면 안 된다");
            Assert.IsFalse(model.State.CurrentValue.IsInRoom);
            Assert.IsNotNull(model.State.CurrentValue.ErrorMessage);

            model.Dispose();
        }

        [Test]
        public void CreateRoom_인텐트의_선택한_MapId가_서비스로_전달된다()
        {
            var service = new FakeDungeonLobbyService
            {
                CurrentRoom = new RoomInfo { RoomId = 9, RoomName = "r", MaxPlayers = 4, Status = RoomStatusType.Waiting, MapId = "dungeon_01" }
            };
            var model = BuildModel(service);

            model.Accept(new LobbyIntent.CreateRoom("r", 2, "dungeon_01"));

            Assert.AreEqual("dungeon_01", service.LastCreateMapId,
                "방 생성 시 선택한 던전 mapId 가 Intent→Model→Repository→Service 로 전파돼야 한다");

            model.Dispose();
        }

        [Test]
        public void RestoreRoom_방이_Waiting이면_NavigateToRoom을_발행한다()
        {
            var service = new FakeDungeonLobbyService
            {
                CurrentRoom = new RoomInfo
                {
                    RoomId = 2,
                    RoomName = "live-room",
                    MaxPlayers = 4,
                    Status = RoomStatusType.Waiting
                }
            };
            var model = BuildModel(service);

            long navigatedRoomId = -1;
            using var _ = model.NavigateToRoom.Subscribe(id => navigatedRoomId = id);

            model.Accept(new LobbyIntent.RestoreRoom(2));

            Assert.AreEqual(2, navigatedRoomId);
            Assert.IsTrue(model.State.CurrentValue.IsInRoom);

            model.Dispose();
        }
    }
}
