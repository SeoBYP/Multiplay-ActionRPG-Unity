using Shared.Infrastructure.Messages;

namespace Server;

public interface IDungeonResultPublisher
{
    Task EnqueueAsync(DungeonClearMessage message);
}
