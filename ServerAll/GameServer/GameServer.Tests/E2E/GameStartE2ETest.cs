using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using GameServer.API.Interceptors;
using GameServer.API.Services;
using GameServer.Application.Domains.Account;
using GameServer.Application.Domains.Auth;
using GameServer.Application.Domains.Auth.Interfaces;
using GameServer.Application.Domains.Chat.Interfaces;
using GameServer.Application.Domains.DungeonLobby;
using GameServer.Application.Domains.DungeonLobby.Interfaces;
using GameServer.Application.Domains.GameSession;
using GameServer.Application.Domains.GameSession.Interfaces;
using GameServer.Application.Domains.Outbox;
using GameServer.Infrastructure.Domains.GameSession;
using GameServer.Application.Domains.User;
using GameServer.Application.Domains.User.Interfaces;
using GameServer.Application.Common.Interfaces;
using GameServer.Application.Security;
using GameServer.Application.Security.Interface;
using GameServer.Grpc.Auth;
using GameServer.Grpc.DungeonLobby;
using GameServer.Infrastructure.Security;
using GameServer.Tests.Infrastructure.Fakes;
using GameServer.Tests.Infrastructure.Fakes.MessageQueue;
using GameServer.Tests.Infrastructure.Fakes.Repositories;
using GameServer.Tests.Infrastructure.Fakes.Services;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Moq;
using Shared.Infrastructure.MessageQueue;
using Shared.Infrastructure.Messages;
using StackExchange.Redis;
using AuthProto = GameServer.Grpc.Auth.AuthService.AuthServiceClient;
using LobbyProto = GameServer.Grpc.DungeonLobby.DungeonLobbyService.DungeonLobbyServiceClient;

namespace GameServer.Tests.E2E;

public class GameStartE2ETest
{
    [Fact]
    public async Task 방_구독을_열어둔_상태에서_게임_시작_RPC를_호출하면_모든_구독자가_게임_시작_이벤트를_받는다()
    {
        await using var fixture = await TestGameServerHost.CreateAsync();

        var authClient = new AuthProto(fixture.Channel);
        var lobbyClient = new LobbyProto(fixture.Channel);

        var hostLogin = await 회원가입후_로그인한다(fixture, authClient, "host@test.com", "Password123!", "host-device");
        var guestLogin = await 회원가입후_로그인한다(fixture, authClient, "guest@test.com", "Password123!", "guest-device");

        var createRoomResponse = await lobbyClient.CreateRoomAsync(
            new CreateRoomRequest
            {
                RoomName = "E2E Room",
                MaxPlayers = 4
            },
            headers: 인증헤더(hostLogin.AccessToken)).ResponseAsync;

        Assert.True(createRoomResponse.Result.Success);
        var roomId = createRoomResponse.RoomInfo.RoomId;

        var joinRoomResponse = await lobbyClient.JoinRoomAsync(
            new JoinRoomRequest { RoomId = roomId },
            headers: 인증헤더(guestLogin.AccessToken)).ResponseAsync;

        Assert.True(joinRoomResponse.Result.Success);

        using var hostSubscribeCall = lobbyClient.SubscribeRoom(
            new SubscribeRoomRequest { RoomId = roomId },
            headers: 인증헤더(hostLogin.AccessToken));
        using var guestSubscribeCall = lobbyClient.SubscribeRoom(
            new SubscribeRoomRequest { RoomId = roomId },
            headers: 인증헤더(guestLogin.AccessToken));

        await fixture.EventStream.WaitForSubscriberCountAsync(roomId, 2, TimeSpan.FromSeconds(2));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var startRoomTask = lobbyClient.StartRoomAsync(
            new StartRoomRequest { RoomId = roomId },
            headers: 인증헤더(hostLogin.AccessToken)).ResponseAsync;

        var startRoomResponse = await startRoomTask;
        var hostEvent = await WaitForGameSessionEventAsync(hostSubscribeCall.ResponseStream, cts.Token);
        var guestEvent = await WaitForGameSessionEventAsync(guestSubscribeCall.ResponseStream, cts.Token);

        Assert.True(startRoomResponse.Result.Success);
        Assert.NotNull(fixture.GameStartRequestedQueue.LastEnqueuedMessage);

        Assert.NotNull(hostEvent);
        Assert.NotNull(guestEvent);
        Assert.Equal("127.0.0.1", hostEvent.Ip);
        Assert.Equal(12345, hostEvent.Port);
        Assert.Equal("127.0.0.1", guestEvent.Ip);
        Assert.Equal(12345, guestEvent.Port);
        Assert.Equal(roomId, fixture.GameStartRequestedQueue.LastEnqueuedMessage!.RoomId);
        Assert.Equal(new long[] { 1L, 2L }, fixture.GameStartRequestedQueue.LastEnqueuedMessage.PlayerInfos.Select(p => p.UserId).OrderBy(x => x).ToArray());
    }

    private static async Task<LoginResponse> 회원가입후_로그인한다(
        TestGameServerHost fixture,
        AuthProto authClient,
        string email,
        string password,
        string deviceId)
    {
        var registerResponse = await authClient.RegisterAsync(new RegisterRequest
        {
            Email = email,
            Password = password
        }).ResponseAsync;

        Assert.True(registerResponse.Result.Success);
        Assert.NotNull(registerResponse.User);

        var registeredUser = await fixture.UserRepository.GetByPublicIdAsync(registerResponse.User.PublicId);
        Assert.NotNull(registeredUser);
        await fixture.UserProfileRepository.CreateAsync(registeredUser!.UserId, registerResponse.User.PublicId);

        var loginResponse = await authClient.LoginAsync(new LoginRequest
        {
            Email = email,
            Password = password,
            DeviceId = deviceId
        }).ResponseAsync;

        Assert.True(loginResponse.Result.Success);
        Assert.False(string.IsNullOrWhiteSpace(loginResponse.AccessToken));

        return loginResponse;
    }

    private static Metadata 인증헤더(string accessToken)
    {
        return new Metadata
        {
            { "authorization", $"Bearer {accessToken}" }
        };
    }

    private static async Task<GameSessionReadyEvent?> WaitForGameSessionEventAsync(
        IAsyncStreamReader<SubscribeRoomResponse> responseStream,
        CancellationToken ct)
    {
        while (await responseStream.MoveNext(ct))
        {
            if (responseStream.Current.GameSessionEvent is not null)
                return responseStream.Current.GameSessionEvent;
        }

        return null;
    }

    private sealed class TestGameServerHost : IAsyncDisposable
    {
        private readonly WebApplication _app;

        public GrpcChannel Channel { get; }
        public FakeDungeonRoomEventStream EventStream { get; }
        public FakeGameStartRequestedQueue GameStartRequestedQueue { get; }
        public FakeUserRepository UserRepository { get; }
        public FakeUserProfileRepository UserProfileRepository { get; }

        private TestGameServerHost(
            WebApplication app,
            GrpcChannel channel,
            FakeDungeonRoomEventStream eventStream,
            FakeGameStartRequestedQueue gameStartRequestedQueue,
            FakeUserRepository userRepository,
            FakeUserProfileRepository userProfileRepository)
        {
            _app = app;
            Channel = channel;
            EventStream = eventStream;
            GameStartRequestedQueue = gameStartRequestedQueue;
            UserRepository = userRepository;
            UserProfileRepository = userProfileRepository;
        }

        public static async Task<TestGameServerHost> CreateAsync()
        {
            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

            var port = GetFreePort();
            var jwtOptions = new JwtOptions
            {
                Issuer = "GameServer.Tests",
                Audience = "GameClient.Tests",
                Secret = "ThisIsATestSecretKeyForJwt1234567890",
                AccessTokenMinutes = 60,
                RefreshTokenExpirationHours = 24
            };

            var builder = WebApplication.CreateBuilder();

            builder.WebHost.ConfigureKestrel(options =>
            {
                options.ListenLocalhost(port, listen => listen.Protocols = HttpProtocols.Http2);
            });

            builder.Services.AddLogging();
            builder.Services.AddAuthorization();
            builder.Services.AddSingleton(Options.Create(jwtOptions));

            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        ValidIssuer = jwtOptions.Issuer,
                        ValidAudience = jwtOptions.Audience,
                        IssuerSigningKey = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(jwtOptions.Secret))
                    };
                });

            var eventStream = new FakeDungeonRoomEventStream();
            var gameSessionReadyQueue = new InMemoryMessageQueue<GameSessionReadyMessage>();
            var gameStartRequestedQueue = new FakeGameStartRequestedQueue(gameSessionReadyQueue);
            var userRepository = new FakeUserRepository();
            var userProfileRepository = new FakeUserProfileRepository();

            builder.Services.AddSingleton<IUserRepository>(userRepository);
            builder.Services.AddSingleton<IUserCredentialRepository, FakeUserCredentialRepository>();
            builder.Services.AddSingleton<IUserSessionRepository, FakeUserSessionRepository>();
            builder.Services.AddSingleton<IUserProfileRepository>(userProfileRepository);
            builder.Services.AddSingleton<IProfanityFilter, AllowAllProfanityFilter>();
            builder.Services.AddSingleton<IDungeonRoomRepository, FakeDungeonRoomRepository>();
            builder.Services.AddSingleton<IDungeonRoomPlayerRepository, FakeDungeonRoomPlayerRepository>();
            builder.Services.AddSingleton<IOutboxRepository, FakeOutboxRepository>();
            builder.Services.AddSingleton<IDungeonRoomEventStream>(eventStream);
            builder.Services.AddSingleton<IMessageQueue<GameStartRequestedMessage>>(gameStartRequestedQueue);
            builder.Services.AddSingleton<IMessageQueue<GameSessionReadyMessage>>(gameSessionReadyQueue);
            builder.Services.AddSingleton<IGameSessionRepository, FakeGameSessionRepository>();
            builder.Services.AddSingleton<IGameSessionPlayerRepository, FakeGameSessionPlayerRepository>();
            builder.Services.AddSingleton<IGameSessionService, GameSessionService>();
            builder.Services.AddSingleton<IChatSubscriptionService, FakeChatSubscriptionService>();
            builder.Services.AddSingleton<IDungeonLobbySubscriptionService, DungeonLobbySubscriptionService>();

            // GameSessionReadyConsumer가 IConnectionMultiplexer.GetDatabase()를 요구한다.
            // 이 테스트는 Redis를 쓰지 않으므로 Mock으로 충족(GetDatabase→Mock IDatabase, 모든 Redis 호출 no-op).
            var mockRedis = new Mock<IConnectionMultiplexer>();
            mockRedis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(Mock.Of<IDatabase>());
            builder.Services.AddSingleton<IConnectionMultiplexer>(mockRedis.Object);

            builder.Services.AddHostedService<GameSessionReadyConsumer>();

            builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
            builder.Services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();
            builder.Services.AddScoped<IAccountService, AccountService>();
            builder.Services.AddScoped<IAuthService, GameServer.Application.Domains.Auth.AuthService>();
            builder.Services.AddSingleton<IUserProfileService, UserProfileService>();
            // 스탯 전파(2.4): StartGame 이 GetStatsAsync 로 참가자 스탯을 메시지에 채운다. Fake 저장소(신규유저=Lv1 기본).
            builder.Services.AddSingleton<GameServer.Application.Domains.Progression.Interfaces.IProgressionRepository>(new FakeProgressionRepository());
            builder.Services.AddScoped<GameServer.Application.Domains.Progression.Interfaces.IProgressionService, GameServer.Application.Domains.Progression.ProgressionService>();
            // 장비 합산(3.2) 의존 — 이 테스트는 장비 무관(Lv1 base)이라 스텁(0 modifier) 등록.
            builder.Services.AddScoped<GameServer.Application.Domains.Equipment.Interfaces.IEquipmentService, Infrastructure.Fakes.Services.FakeEquipmentService>();
            builder.Services.AddScoped<IDungeonLobbyService, GameServer.Application.Domains.DungeonLobby.DungeonLobbyService>();

            builder.Services.AddScoped<AuthInterceptor>();
            builder.Services.AddGrpc(options =>
            {
                options.EnableDetailedErrors = true;
                options.Interceptors.Add<AuthInterceptor>();
            });

            var app = builder.Build();
            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapGrpcService<GameServer.API.Services.AuthGrpcService>();
            app.MapGrpcService<DungeonLobbyGrpcService>();

            await app.StartAsync();

            var httpHandler = new SocketsHttpHandler
            {
                EnableMultipleHttp2Connections = true
            };

            var channel = GrpcChannel.ForAddress(
                $"http://127.0.0.1:{port}",
                new GrpcChannelOptions
                {
                    HttpHandler = httpHandler
                });

            return new TestGameServerHost(app, channel, eventStream, gameStartRequestedQueue, userRepository, userProfileRepository);
        }

        public async ValueTask DisposeAsync()
        {
            Channel.Dispose();
            await _app.StopAsync();
            await _app.DisposeAsync();
        }

        private static int GetFreePort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }
    }
}
