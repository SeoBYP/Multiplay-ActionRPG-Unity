using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using GameServer.API.Interceptors;
using GameServer.API.Services;
using GameServer.Application.Domains.Auth;
using GameServer.Application.Domains.Auth.Interfaces;
using GameServer.Application.Domains.Chat.Interfaces;
using GameServer.Application.Domains.DungeonLobby;
using GameServer.Application.Domains.DungeonLobby.Interfaces;
using GameServer.Application.Domains.User;
using GameServer.Application.Domains.User.Interfaces;
using GameServer.Application.Security;
using GameServer.Application.Security.Interface;
using GameServer.Grpc.Auth;
using GameServer.Grpc.DungeonLobby;
using GameServer.Infrastructure.Security;
using GameServer.Tests.Fakes;
using GameServer.Tests.Infrastructure;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Shared.Infrastructure.Messages;
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

        var hostLogin = await 회원가입후_로그인한다(authClient, "host@test.com", "Password123!", "host-device");
        var guestLogin = await 회원가입후_로그인한다(authClient, "guest@test.com", "Password123!", "guest-device");

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
        var hostMoveNextTask = hostSubscribeCall.ResponseStream.MoveNext(cts.Token);
        var guestMoveNextTask = guestSubscribeCall.ResponseStream.MoveNext(cts.Token);
        var startRoomTask = lobbyClient.StartRoomAsync(
            new StartRoomRequest { RoomId = roomId },
            headers: 인증헤더(hostLogin.AccessToken)).ResponseAsync;

        var startRoomResponse = await startRoomTask;
        var hostReceived = await hostMoveNextTask;
        var guestReceived = await guestMoveNextTask;

        Assert.True(startRoomResponse.Result.Success);
        Assert.True(hostReceived);
        Assert.True(guestReceived);
        Assert.NotNull(fixture.GameStartPublisher.LastPublishedMessage);

        var hostEvent = hostSubscribeCall.ResponseStream.Current.StartEvent;
        var guestEvent = guestSubscribeCall.ResponseStream.Current.StartEvent;

        Assert.NotNull(hostEvent);
        Assert.NotNull(guestEvent);
        Assert.Equal("127.0.0.1", hostEvent.Ip);
        Assert.Equal(12345, hostEvent.Port);
        Assert.Equal("127.0.0.1", guestEvent.Ip);
        Assert.Equal(12345, guestEvent.Port);
        Assert.Equal(roomId, hostEvent.RoomInfo.RoomId);
        Assert.Equal(roomId, guestEvent.RoomInfo.RoomId);
        Assert.Equal(RoomStatusType.Playing, hostEvent.RoomInfo.Status);
        Assert.Equal(RoomStatusType.Playing, guestEvent.RoomInfo.Status);
        Assert.Equal(roomId, fixture.GameStartPublisher.LastPublishedMessage!.RoomId);
        Assert.Equal(new long[] { 1L, 2L }, fixture.GameStartPublisher.LastPublishedMessage.PlayerIds.OrderBy(x => x).ToArray());
    }

    private static async Task<LoginResponse> 회원가입후_로그인한다(
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

    private sealed class TestGameServerHost : IAsyncDisposable
    {
        private readonly WebApplication _app;

        public GrpcChannel Channel { get; }
        public FakeDungeonRoomEventStream EventStream { get; }
        public FakeGameStartPublisher GameStartPublisher { get; }

        private TestGameServerHost(
            WebApplication app,
            GrpcChannel channel,
            FakeDungeonRoomEventStream eventStream,
            FakeGameStartPublisher gameStartPublisher)
        {
            _app = app;
            Channel = channel;
            EventStream = eventStream;
            GameStartPublisher = gameStartPublisher;
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
            var gameStartPublisher = new FakeGameStartPublisher();

            builder.Services.AddSingleton<IUserRepository, FakeUserRepository>();
            builder.Services.AddSingleton<IUserSessionRepository, FakeUserSessionRepository>();
            builder.Services.AddSingleton<IDungeonRoomRepository, FakeDungeonRoomRepository>();
            builder.Services.AddSingleton<IDungeonRoomEventStream>(eventStream);
            builder.Services.AddSingleton<IGameStartPublisher>(gameStartPublisher);
            builder.Services.AddSingleton<ISocketReadyChecker>(new FakeSocketReadyChecker("127.0.0.1:12345"));
            builder.Services.AddSingleton<IChatSubscriptionService, FakeChatSubscriptionService>();
            builder.Services.AddSingleton<IDungeonLobbySubscriptionService, DungeonLobbySubscriptionService>();

            builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
            builder.Services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();
            builder.Services.AddScoped<IAuthService, GameServer.Application.Domains.Auth.AuthService>();
            builder.Services.AddSingleton<IUserService, UserService>();
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

            return new TestGameServerHost(app, channel, eventStream, gameStartPublisher);
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

    private sealed class FakeGameStartPublisher : IGameStartPublisher
    {
        public GameStartMessage? LastPublishedMessage { get; private set; }

        public Task PublishAsync(GameStartMessage message, CancellationToken ct = default)
        {
            LastPublishedMessage = new GameStartMessage
            {
                RoomId = message.RoomId,
                PlayerIds = [.. message.PlayerIds]
            };
            return Task.CompletedTask;
        }
    }

    private sealed class FakeSocketReadyChecker(string socketInfo) : ISocketReadyChecker
    {
        public Task<string?> WaitAsync(long roomId, CancellationToken ct = default)
        {
            return Task.FromResult<string?>(socketInfo);
        }
    }

    private sealed class FakeDungeonRoomEventStream : IDungeonRoomEventStream
    {
        private readonly ConcurrentDictionary<long, ConcurrentDictionary<Guid, Channel<long>>> _channels = new();

        public Task PublishAsync(long roomId, CancellationToken ct = default)
        {
            if (_channels.TryGetValue(roomId, out var subscribers))
            {
                foreach (var channel in subscribers.Values)
                {
                    channel.Writer.TryWrite(roomId);
                }
            }

            return Task.CompletedTask;
        }

        public async IAsyncEnumerable<long> ReadAsync(
            long roomId,
            string lastEventId,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            var id = Guid.NewGuid();
            var channel = Channel.CreateUnbounded<long>();
            var subscribers = _channels.GetOrAdd(roomId, _ => new ConcurrentDictionary<Guid, Channel<long>>());
            subscribers[id] = channel;

            try
            {
                await foreach (var value in channel.Reader.ReadAllAsync(ct))
                {
                    yield return value;
                }
            }
            finally
            {
                if (_channels.TryGetValue(roomId, out var roomSubscribers))
                {
                    roomSubscribers.TryRemove(id, out _);
                    if (roomSubscribers.IsEmpty)
                    {
                        _channels.TryRemove(roomId, out _);
                    }
                }
            }
        }

        public async Task WaitForSubscriberCountAsync(long roomId, int expectedCount, TimeSpan timeout)
        {
            using var cts = new CancellationTokenSource(timeout);

            try
            {
                while (!cts.IsCancellationRequested)
                {
                    if (_channels.TryGetValue(roomId, out var subscribers) && subscribers.Count >= expectedCount)
                    {
                        return;
                    }

                    await Task.Delay(10, cts.Token);
                }
            }
            catch (OperationCanceledException)
            {
            }

            throw new TimeoutException($"roomId={roomId} subscriber count did not reach {expectedCount}.");
        }
    }
}

