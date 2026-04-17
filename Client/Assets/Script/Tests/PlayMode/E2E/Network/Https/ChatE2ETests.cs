using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Network.Https.Core;
using Game.Network.Https.Services;
using GameServer.Grpc.Auth;
using GameServer.Grpc.Chat;
using GameServer.Grpc.User;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode.E2E
{
    [TestFixture]
    public class ChatE2ETests : E2ETestBase
    {
        [UnityTest]
        public System.Collections.IEnumerator ChatStream_글로벌_메시지_수신() => UniTask.ToCoroutine(async () =>
        {
            await RegisterLoginAndSetNicknameAsync(UniqueEmail(), "Test1234!", UniqueNickname("Global"));

            var received = new List<ChatServerMessage>();
            var outgoing = new TestObservable<ChatClientMessage>();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            var streamTask = ChatService.ChatStreamAsync(outgoing, msg => received.Add(msg), cts.Token).AsTask();
            await UniTask.Delay(250);

            outgoing.Publish(new ChatClientMessage
            {
                Chat = new ChatPayload
                {
                    Message = "global hello"
                }
            });

            await UniTask.WaitUntil(() => TryFindChatMessage(received, "global hello", out _), cancellationToken: Timeout());
            cts.Cancel();

            try
            {
                await streamTask;
            }
            catch (OperationCanceledException)
            {
            }
            catch (Grpc.Core.RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.Cancelled)
            {
            }

            Assert.GreaterOrEqual(received.Count, 1);
            Assert.IsTrue(TryFindChatMessage(received, "global hello", out var globalMessage));
            Assert.IsNotNull(globalMessage.Chat);
            Assert.AreEqual("global hello", globalMessage.Chat.Message);
        });

        [UnityTest]
        public System.Collections.IEnumerator ChatStream_방_채팅_수신() => UniTask.ToCoroutine(async () =>
        {
            await RegisterLoginAndSetNicknameAsync(UniqueEmail(), "Test1234!", UniqueNickname("Room"));

            var created = await LobbyService.CreateRoomAsync(new GameServer.Grpc.DungeonLobby.CreateRoomRequest
            {
                RoomName = "Chat Room",
                MaxPlayers = 2
            }, Timeout());
            Assert.IsTrue(created.Result.Success, created.Result.Message);

            var received = new List<ChatServerMessage>();
            var outgoing = new TestObservable<ChatClientMessage>();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            var streamTask = ChatService.ChatStreamAsync(outgoing, msg => received.Add(msg), cts.Token).AsTask();
            await UniTask.Delay(250);

            outgoing.Publish(new ChatClientMessage
            {
                Chat = new ChatPayload
                {
                    Message = "room hello"
                }
            });

            await UniTask.WaitUntil(() => TryFindChatMessage(received, "room hello", out _), cancellationToken: Timeout());
            cts.Cancel();

            try
            {
                await streamTask;
            }
            catch (OperationCanceledException)
            {
            }
            catch (Grpc.Core.RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.Cancelled)
            {
            }

            Assert.GreaterOrEqual(received.Count, 1);
            Assert.IsTrue(TryFindChatMessage(received, "room hello", out var roomMessage));
            Assert.IsNotNull(roomMessage.Chat);
            Assert.AreEqual("room hello", roomMessage.Chat.Message);
            Assert.AreEqual(created.RoomInfo.RoomId, roomMessage.Chat.RoomId);
        });

        [UnityTest]
        public System.Collections.IEnumerator ChatStream_귓속말_대상자_수신() => UniTask.ToCoroutine(async () =>
        {
            var targetProvider = new GrpcChannelProvider(ServerConfig.GameServerGrpcAddress);
            var senderProvider = new GrpcChannelProvider(ServerConfig.GameServerGrpcAddress);

            try
            {
                var targetAuth = new AuthGrpcService(targetProvider);
                var targetUser = new UserGrpcService(targetProvider);
                var targetChat = new ChatGrpcService(targetProvider);

                var senderAuth = new AuthGrpcService(senderProvider);
                var senderUser = new UserGrpcService(senderProvider);
                var senderChat = new ChatGrpcService(senderProvider);

                var targetNickname = UniqueNickname("Target");
                var senderNickname = UniqueNickname("Sender");

                await RegisterLoginAndConfigureClientAsync(targetProvider, targetAuth, targetUser, UniqueEmail(), targetNickname);
                await RegisterLoginAndConfigureClientAsync(senderProvider, senderAuth, senderUser, UniqueEmail(), senderNickname);

                var received = new List<ChatServerMessage>();
                using var receiveCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                using var sendCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

                var receiverOutgoing = new TestObservable<ChatClientMessage>();
                var senderOutgoing = new TestObservable<ChatClientMessage>();

                var receiveTask = targetChat.ChatStreamAsync(receiverOutgoing, msg => received.Add(msg), receiveCts.Token).AsTask();
                await UniTask.Delay(250);

                var sendTask = senderChat.ChatStreamAsync(senderOutgoing, _ => { }, sendCts.Token).AsTask();
                await UniTask.Delay(250);

                senderOutgoing.Publish(new ChatClientMessage
                {
                    Chat = new ChatPayload
                    {
                        Message = "secret hello",
                        TargetUserNickname = targetNickname
                    }
                });

                await UniTask.WaitUntil(() => TryFindChatMessage(received, "secret hello", out _), cancellationToken: Timeout());
                sendCts.Cancel();
                receiveCts.Cancel();

                try
                {
                    await sendTask;
                }
                catch (OperationCanceledException)
                {
                }
                catch (Grpc.Core.RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.Cancelled)
                {
                }

                try
                {
                    await receiveTask;
                }
                catch (OperationCanceledException)
                {
                }
                catch (Grpc.Core.RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.Cancelled)
                {
                }

                Assert.GreaterOrEqual(received.Count, 1);
                Assert.IsTrue(TryFindChatMessage(received, "secret hello", out var whisperMessage));
                Assert.AreEqual("secret hello", whisperMessage.Chat.Message);
                Assert.AreEqual(targetNickname, whisperMessage.Chat.TargetUserNickname);
                Assert.AreEqual(senderNickname, whisperMessage.Chat.SenderNickname);
            }
            finally
            {
                targetProvider.Dispose();
                senderProvider.Dispose();
            }
        });

        private static bool TryFindChatMessage(List<ChatServerMessage> received, string expectedMessage, out ChatServerMessage matched)
        {
            foreach (var message in received)
            {
                if (message.Chat is not null && message.Chat.Message == expectedMessage)
                {
                    matched = message;
                    return true;
                }
            }

            matched = null;
            return false;
        }

        private static async UniTask RegisterLoginAndConfigureClientAsync(
            GrpcChannelProvider provider,
            AuthGrpcService authService,
            UserGrpcService userService,
            string email,
            string nickname)
        {
            var register = await authService.RegisterAsync(new RegisterRequest
            {
                Email = email,
                Password = "Test1234!"
            }, Timeout());
            Assert.IsTrue(register.Result.Success, register.Result.Message);

            var login = await authService.LoginAsync(new LoginRequest
            {
                Email = email,
                Password = "Test1234!",
                DeviceId = "e2e-device"
            }, Timeout());
            Assert.IsTrue(login.Result.Success, login.Result.Message);

            provider.AccessTokenProvider = () => login.AccessToken;

            var nicknameResult = await userService.SetNickNameAsync(new SetNicknameRequest
            {
                Nickname = nickname
            }, Timeout());
            Assert.IsTrue(nicknameResult.Result.Success, nicknameResult.Result.Message);
        }

        private sealed class TestObservable<T> : IObservable<T>
        {
            private readonly List<IObserver<T>> _observers = new List<IObserver<T>>();

            public IDisposable Subscribe(IObserver<T> observer)
            {
                _observers.Add(observer);
                return new Subscription(_observers, observer);
            }

            public void Publish(T value)
            {
                foreach (var observer in _observers.ToArray())
                    observer.OnNext(value);
            }

            private sealed class Subscription : IDisposable
            {
                private readonly List<IObserver<T>> _observers;
                private readonly IObserver<T> _observer;

                public Subscription(List<IObserver<T>> observers, IObserver<T> observer)
                {
                    _observers = observers;
                    _observer = observer;
                }

                public void Dispose()
                {
                    _observers.Remove(_observer);
                }
            }
        }
    }
}
