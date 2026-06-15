using GameServer.Application.Domains.Equipment.Interfaces;
using GameServer.Domain.Entities.Equipment;
using Shared.Gameplay.Equipment;

namespace GameServer.Tests.Infrastructure.Fakes.Repositories;

/// <summary>실제 EquipmentRepository 의 착용 upsert/clear 동작을 인메모리로 모사.</summary>
public class FakeEquipmentRepository : IEquipmentRepository
{
    private readonly Dictionary<(long UserId, EquipmentType Slot), string> _equipped = new();

    public Task<List<UserEquipment>> GetEquippedAsync(long userId, CancellationToken ct = default)
        => Task.FromResult(_equipped
            .Where(kv => kv.Key.UserId == userId)
            .Select(kv => UserEquipment.Create(userId, kv.Key.Slot, kv.Value))
            .ToList());

    public Task SetAsync(long userId, EquipmentType slot, string itemId, CancellationToken ct = default)
    {
        _equipped[(userId, slot)] = itemId;
        return Task.CompletedTask;
    }

    public Task<bool> ClearAsync(long userId, EquipmentType slot, CancellationToken ct = default)
        => Task.FromResult(_equipped.Remove((userId, slot)));
}
