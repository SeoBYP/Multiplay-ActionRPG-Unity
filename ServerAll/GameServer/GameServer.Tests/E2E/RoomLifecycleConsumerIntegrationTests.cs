using GameServer.Application.Domains.Chat.Interfaces;
using GameServer.Application.Domains.DungeonLobby;
using GameServer.Application.Domains.DungeonLobby.Interfaces;
using GameServer.Application.Domains.Outbox;
using GameServer.Application.Domains.User.Interfaces;
using GameServer.Infrastructure.Common.Consumer;
using GameServer.Tests.Infrastructure.Fakes.MessageQueue;
using GameServer.Tests.Infrastructure.Fakes.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using Shared.Infrastructure.MessageQueue;
using Shared.Infrastructure.Messages;

namespace GameServer.Tests.E2E;

/// <summary>
/// PlayerLeft 이벤트 소비 파이프라인 통합 테스트.
///
/// 실제 RoomLifecycleConsumer(BackgroundService)를 띄우고 InMemoryMessageQueue로 메시지를 흘려
/// "발행 → 소비 → RemovePlayerFromRoomAsync → DB 상태" 끝단까지 검증한다.
/// (원래 버그의 본질인 "consumer 미배선/타입 불일치"를 잡는 계층)
/// </summary>
public class RoomLifecycleConsumerIntegrationTests
{
    private sealed class Harness
    {
        public required ServiceProvider Provider { get; init; }
        public required FakeDungeonRoomRepository Rooms { get; init; }
        public required FakeDungeonRoomPlayerRepository Players { get; init; }
        public required FakeUserSessionRepository Sessions { get; init; }
        public required InMemoryMessageQueue<PlayerLeftRoomMessage> Queue { get; init; }
        public required RoomLifecycleConsumer Consumer { get; init; }
    }

    private static Harness BuildHarness()
    {
        var rooms = new FakeDungeonRoomRepository();
        var players = new FakeDungeonRoomPlayerRepository();
        var sessions = new FakeUserSessionRepository();
        var queue = new InMemoryMessageQueue<PlayerLeftRoomMessage>();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IDungeonRoomRepository>(rooms);
        services.AddSingleton<IDungeonRoomPlayerRepository>(players);
        services.AddSingleton<IUserSessionRepository>(sessions);
        services.AddSingleton<IUserProfileRepository>(new FakeUserProfileRepository());
        services.AddSingleton(Mock.Of<IChatSubscriptionService>());
        services.AddSingleton(Mock.Of<IDungeonLobbySubscriptionService>());
        services.AddSingleton(Mock.Of<IOutboxRepository>());
        services.AddSingleton<IMessageQueue<PlayerLeftRoomMessage>>(queue);
        services.AddScoped<IDungeonLobbyService, DungeonLobbyService>();
        services.AddSingleton<RoomLifecycleConsumer>();

        var provider = services.BuildServiceProvider();
        return new Harness
        {
            Provider = provider,
            Rooms = rooms,
            Players = players,
            Sessions = sessions,
            Queue = queue,
            Consumer = provider.GetRequiredService<RoomLifecycleConsumer>()
        };
    }

    private static async Task<long> SeedRoomAsync(Harness h, long hostUserId, params long[] guestUserIds)
    {
        using var scope = h.Provider.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IDungeonLobbyService>();
        var hostSession = await h.Sessions.CreateSessionAsync(hostUserId);
        var created = await svc.CreateDungeonRoomAsync(hostSession!.SessionId, "Room", 4);
        foreach (var guest in guestUserIds)
        {
            var gs = await h.Sessions.CreateSessionAsync(guest);
            await svc.JoinRoomAsync(gs!.SessionId, created.Value!.RoomId);
        }
        return created.Value!.RoomId;
    }

    private static async Task WaitUntilAsync(Func<Task<bool>> condition, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            if (await condition()) return;
            await Task.Delay(20, ct);
        }
        ct.ThrowIfCancellationRequested();
    }

    [Fact]
    public async Task PlayerLeft_소비시_마지막_플레이어면_방삭제_및_association제거()
    {
        var h = BuildHarness();
        var roomId = await SeedRoomAsync(h, hostUserId: 1);
        Assert.NotNull(await h.Players.GetByUserIdAsync(1));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await h.Consumer.StartAsync(cts.Token);

        await h.Queue.EnqueueAsync(new PlayerLeftRoomMessage { RoomId = roomId, UserId = 1, RoomEmptied = true });

        // 최종 상태(방 삭제)를 기다린다 — association 제거는 그 전 단계라 별도로 폴링하면 레이스.
        await WaitUntilAsync(async () => await h.Rooms.GetByIdAsync(roomId) is null, cts.Token);

        Assert.Null(await h.Players.GetByUserIdAsync(1));          // association 제거 → CurrentRoomId=0
        Assert.Null(await h.Rooms.GetByIdAsync(roomId));           // 빈 방 → 삭제

        await h.Consumer.StopAsync(CancellationToken.None);
        await h.Provider.DisposeAsync();
    }

    [Fact]
    public async Task PlayerLeft_소비시_부분퇴장이면_떠난사람만_제거_남은사람_유지()
    {
        var h = BuildHarness();
        var roomId = await SeedRoomAsync(h, hostUserId: 1, guestUserIds: 2);
        Assert.NotNull(await h.Players.GetByUserIdAsync(2));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await h.Consumer.StartAsync(cts.Token);

        await h.Queue.EnqueueAsync(new PlayerLeftRoomMessage { RoomId = roomId, UserId = 2, RoomEmptied = false });

        await WaitUntilAsync(async () => await h.Players.GetByUserIdAsync(2) is null, cts.Token);

        Assert.Null(await h.Players.GetByUserIdAsync(2));          // 떠난 사람만 제거
        Assert.NotNull(await h.Players.GetByUserIdAsync(1));       // 남은 사람 유지 → 복원 가능
        Assert.NotNull(await h.Rooms.GetByIdAsync(roomId));        // 방 유지

        await h.Consumer.StopAsync(CancellationToken.None);
        await h.Provider.DisposeAsync();
    }
}
