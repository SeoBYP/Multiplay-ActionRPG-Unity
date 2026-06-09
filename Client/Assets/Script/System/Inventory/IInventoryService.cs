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

        /// <summary>소모품 차감(서버 권위). 성공 시 남은 수량. proto 는 여기서 숨긴다(Presentation 비노출).</summary>
        UniTask<(InventoryResult Result, int Remaining)> ConsumeItemAsync(string itemId, int qty, CancellationToken ct = default);
    }
}
