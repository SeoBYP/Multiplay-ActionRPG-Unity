using System;
using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Network.Https.Core;
using Game.Network.Https.Services;
using Game.Presentation.Chat;
using Game.System.Auth;
using Game.System.DungeonLobby;
using GameServer.Grpc.Auth;
using GameServer.Grpc.DungeonLobby;
using GameServer.Grpc.User;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode.E2E
{
    /// <summary>
    /// **채팅 채널 결정을 실서버(Docker)로 고정한다.**
    ///
    /// HUD 채널 드롭다운은 항목을 상황에 따라 바꾼다(Main = 전체·개인 / 방·던전 = 방·개인).
    /// 그 전제는 "서버가 방 소속 여부로 전체/방을 정한다"는 것 — 전제가 서버에서 바뀌면
    /// 드롭다운은 **고를 수 없는 항목을 보여주는 거짓말**이 된다. 그래서 규칙 자체를 여기서 못 박는다.
    ///
    /// 목(mock) 없이 <see cref="ChatModel"/> 을 그대로 쓴다 — 화면이 쓰는 것과 같은 코드 경로.
    /// </summary>
    [TestFixture]
    public class ChatChannelE2ETests : E2ETestBase
    {
        [UnityTest]
        public IEnumerator 방_밖에서_보낸_말은_전체_채널로_돌아온다() => UniTask.ToCoroutine(async () =>
        {
            await RegisterLoginAndSetNicknameAsync(UniqueEmail(), "Test1234!", UniqueNickname("Solo"));

            var lobby = new DungeonLobbySession();
            using var model = CreateModel(ChatService, AccessToken, lobby);
            await UniTask.Delay(400); // 스트림 연결

            Assert.IsFalse(model.IsInRoom, "방에 들어가지 않았으므로 일반 채널은 '전체' 여야 한다.");
            Assert.IsTrue(model.Send("전체 채널 확인"));

            var line = await WaitForLineAsync(model, "전체 채널 확인");
            Assert.AreEqual(ChatChannel.Global, line.Channel);
        });

        [UnityTest]
        public IEnumerator 방에_들어가면_같은_말이_방_채널로_돌아온다() => UniTask.ToCoroutine(async () =>
        {
            await RegisterLoginAndSetNicknameAsync(UniqueEmail(), "Test1234!", UniqueNickname("Room"));

            var created = await LobbyService.CreateRoomAsync(new CreateRoomRequest
            {
                RoomName = "Chat Channel Room",
                MaxPlayers = 2
            }, Timeout());
            Assert.IsTrue(created.Result.Success, created.Result.Message);

            // 클라 쪽 방 소속 진실원(대기실). 드롭다운은 이 값으로 '방' 항목을 띄운다.
            var lobby = new DungeonLobbySession();
            lobby.SetRoom(created.RoomInfo);

            using var model = CreateModel(ChatService, AccessToken, lobby);
            await UniTask.Delay(400);

            Assert.IsTrue(model.IsInRoom, "방 생성·입장 후에는 일반 채널이 '방' 이어야 한다.");
            Assert.IsTrue(model.Send("방 채널 확인"));

            var line = await WaitForLineAsync(model, "방 채널 확인");
            Assert.AreEqual(ChatChannel.Room, line.Channel,
                "서버가 방 소속을 보고 Room 으로 정해야 드롭다운의 '방' 표기가 사실이 된다.");
        });

        [UnityTest]
        public IEnumerator 개인_채널로_보내면_상대에게_귓속말로_도착한다() => UniTask.ToCoroutine(async () =>
        {
            var receiverProvider = new GrpcChannelProvider(ServerConfig.GameServerGrpcAddress);
            var senderProvider   = new GrpcChannelProvider(ServerConfig.GameServerGrpcAddress);

            try
            {
                var receiverNickname = UniqueNickname("Recv");
                var receiverToken = await RegisterLoginAndConfigureAsync(receiverProvider, UniqueEmail(), receiverNickname);
                var senderToken   = await RegisterLoginAndConfigureAsync(senderProvider, UniqueEmail(), UniqueNickname("Send"));

                using var receiver = CreateModel(new ChatGrpcService(receiverProvider), receiverToken, new DungeonLobbySession());
                using var sender   = CreateModel(new ChatGrpcService(senderProvider),   senderToken,   new DungeonLobbySession());
                await UniTask.Delay(500);

                // 드롭다운 '개인' 선택과 같은 호출 — 첫 단어가 받는 사람이다.
                Assert.IsTrue(sender.Send($"{receiverNickname} 귓속말 확인", whisperMode: true));

                var line = await WaitForLineAsync(receiver, "귓속말 확인");
                Assert.AreEqual(ChatChannel.Whisper, line.Channel);
            }
            finally
            {
                receiverProvider.Dispose();
                senderProvider.Dispose();
            }
        });

        // ── 헬퍼 ────────────────────────────────

        /// <summary>화면이 쓰는 것과 같은 Model. 입력 점유·소켓은 이 테스트의 관심사가 아니라 널.</summary>
        private static ChatModel CreateModel(
            Game.Network.Https.Interfaces.IChatGrpcService chat, string accessToken, DungeonLobbySession lobby)
        {
            var auth = new AuthSession(new NoopTokenStore());
            auth.Update(accessToken, "refresh", 0); // AuthenticatedAsync 를 통과시킨다

            var model = new ChatModel(chat, auth, inputContext: null, lobbySession: lobby, socketSession: null);
            model.StartAsync(CancellationToken.None).Forget();
            return model;
        }

        private static async UniTask<ChatLine> WaitForLineAsync(ChatModel model, string contains)
        {
            ChatLine found = default;
            await UniTask.WaitUntil(() =>
            {
                foreach (var line in model.Recent)
                {
                    if (!line.Text.Contains(contains)) continue;
                    found = line;
                    return true;
                }
                return false;
            }, cancellationToken: Timeout());

            return found;
        }

        private static async UniTask<string> RegisterLoginAndConfigureAsync(
            GrpcChannelProvider provider, string email, string nickname)
        {
            var authService = new AuthGrpcService(provider);
            var userService = new UserGrpcService(provider);

            var register = await authService.RegisterAsync(new RegisterRequest
            {
                Email = email, Password = "Test1234!"
            }, Timeout());
            Assert.IsTrue(register.Result.Success, register.Result.Message);

            var login = await authService.LoginAsync(new LoginRequest
            {
                Email = email, Password = "Test1234!", DeviceId = "e2e-device"
            }, Timeout());
            Assert.IsTrue(login.Result.Success, login.Result.Message);

            provider.AccessTokenProvider = () => login.AccessToken;

            var nicknameResult = await userService.SetNickNameAsync(new SetNicknameRequest
            {
                Nickname = nickname
            }, Timeout());
            Assert.IsTrue(nicknameResult.Result.Success, nicknameResult.Result.Message);

            return login.AccessToken;
        }

        /// <summary>전역 PlayerPrefs 를 건드리지 않는다 — 테스트끼리 토큰을 공유하면 서로를 오염시킨다.</summary>
        private sealed class NoopTokenStore : ITokenStore
        {
            public void Save(string accessToken, string refreshToken, long expiresAt) { }
            public bool TryLoad(out string accessToken, out string refreshToken, out long expiresAt)
            {
                accessToken = null; refreshToken = null; expiresAt = 0;
                return false;
            }
            public void Clear() { }
        }
    }
}
