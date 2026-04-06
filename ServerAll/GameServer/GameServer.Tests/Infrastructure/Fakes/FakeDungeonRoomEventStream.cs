using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using GameServer.Application.Domains.DungeonLobby.Interfaces;

namespace GameServer.Tests.Infrastructure.Fakes;

public sealed class FakeDungeonRoomEventStream : IDungeonRoomEventStream
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
