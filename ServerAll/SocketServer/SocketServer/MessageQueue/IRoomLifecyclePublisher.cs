using Shared.Infrastructure.Messages;

namespace Server;

public interface IRoomLifecyclePublisher
{
    Task EnqueueAsync(PlayerLeftRoomMessage message);
}
