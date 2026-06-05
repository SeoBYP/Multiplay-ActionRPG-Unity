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

            public UniTask<(DungeonLobbyResult, IReadOnlyList<RoomInfo>)> GetRoomsAsync(CancellationToken ct = default)
                => UniTask.FromResult((DungeonLobbyResult.Success, (IReadOnlyList<RoomInfo>)new List<RoomInfo>()));

            public UniTask<DungeonLobbyResult> CreateRoomAsync(string roomName, int maxPlayers, CancellationToken ct = default)
                => UniTask.FromResult(DungeonLobbyResult.Success);

            public UniTask<DungeonLobbyResult> JoinRoomAsync(long roomId, CancellationToken ct = default)
                => UniTask.FromResult(DungeonLobbyResult.Success);

            public UniTask<DungeonLobbyResult> LeaveRoomAsync(CancellationToken ct = default)
                => UniTask.FromResult(DungeonLobbyResult.Success);

            public UniTask<DungeonLobbyResult> StartGameAsync(CancellationToken ct = default)
                => UniTask.FromResult(DungeonLobbyResult.Success);

            public UniTask<DungeonLobbyResult> RestoreRoomAsync(long roomId, CancellationToken ct = default)
                => UniTask.FromResult(DungeonLobbyResult.Success);
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
            return new LobbyModel(repository, service, new FakeAuthService(), new StartupIntentQueue(), new FakeInputContext());
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
