using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Game.System.Inventory
{
    /// <summary>
    /// 인벤토리 조회 오케스트레이션. gRPC 결과를 InventoryResult + 도메인 DTO로 정규화한다.
    /// </summary>
    public interface IInventoryService
    {
        UniTask<(InventoryResult Result, IReadOnlyList<InventoryItemData> Items)> GetInventoryAsync(CancellationToken ct = default);
    }
}
