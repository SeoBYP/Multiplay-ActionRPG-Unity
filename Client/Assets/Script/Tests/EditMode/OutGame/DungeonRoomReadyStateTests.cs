using Game.Presentation.DungeonLobby;
using GameServer.Grpc.DungeonLobby;
using GameServer.Grpc.User;
using NUnit.Framework;

namespace Game.Tests.EditMode.OutGame
{
    /// <summary>
    /// 대기실 준비 상태의 클라 매핑 규칙.
    /// 서버는 플레이어를 public_id 로만 노출하므로 "방장 / 나 / 준비 여부" 판정이 전부 이 키로 이뤄진다.
    /// </summary>
    public class DungeonRoomReadyStateTests
    {
        private static RoomInfo BuildRoom(string hostPublicId, params (string PublicId, bool Ready)[] players)
        {
            var info = new RoomInfo
            {
                RoomId       = 1,
                RoomName     = "room",
                MaxPlayers   = 4,
                HostPublicId = hostPublicId,
                Status       = RoomStatusType.Waiting,
            };

            foreach (var (publicId, ready) in players)
            {
                info.CurrentPlayers.Add(new UserInfo { PublicId = publicId, NickName = publicId });
                if (ready) info.ReadyPublicIds.Add(publicId);
            }

            return info;
        }

        [Test]
        public void 방장은_준비목록에_없어도_준비된_것으로_본다()
        {
            var model = new DungeonRoomModel(BuildRoom("host", ("host", false), ("guest", true)));

            Assert.IsTrue(model.IsHost("host"));
            Assert.IsTrue(model.IsReady("host"));
        }

        [Test]
        public void 비방장이_전원_준비하면_AllOthersReady가_참이다()
        {
            var model = new DungeonRoomModel(
                BuildRoom("host", ("host", false), ("a", true), ("b", true)));

            Assert.IsTrue(model.AllOthersReady);
        }

        [Test]
        public void 비방장이_한명이라도_미준비면_AllOthersReady가_거짓이다()
        {
            var model = new DungeonRoomModel(
                BuildRoom("host", ("host", false), ("a", true), ("b", false)));

            Assert.IsFalse(model.AllOthersReady);
            Assert.IsFalse(model.IsReady("b"));
        }

        [Test]
        public void 방장_혼자면_준비_대상이_없어_AllOthersReady가_참이다()
        {
            var model = new DungeonRoomModel(BuildRoom("host", ("host", false)));

            Assert.IsTrue(model.AllOthersReady);
        }

        [Test]
        public void 내가_방장이_아니면_IsHost가_거짓이다()
        {
            var model = new DungeonRoomModel(BuildRoom("host", ("host", false), ("guest", false)));

            Assert.IsFalse(model.IsHost("guest"));
        }

        [Test]
        public void 인증으로_받은_내_PublicId가_State에_유지된다()
        {
            var state = LobbyState.Initial.WithIdentity("me");

            var afterJoin = state.WithRoomJoined(BuildRoom("host", ("host", false), ("me", false)));

            // 방 입장/갱신을 거쳐도 내 식별자는 남아야 슬롯 판정이 계속 동작한다.
            Assert.AreEqual("me", afterJoin.MyPublicId);
            Assert.AreEqual("me", afterJoin.WithLoading().MyPublicId);
            Assert.AreEqual("me", afterJoin.WithRoomLeft().MyPublicId);
        }
    }
}
