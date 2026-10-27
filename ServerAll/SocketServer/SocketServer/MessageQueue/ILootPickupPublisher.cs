using Shared.Infrastructure.Messages;

namespace Server;

public interface ILootPickupPublisher
{
    Task EnqueueAsync(ItemPickedUpMessage message);
}
