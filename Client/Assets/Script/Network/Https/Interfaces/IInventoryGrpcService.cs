using System.Threading;
using Cysharp.Threading.Tasks;
using GameServer.Grpc.Inventory;

namespace Game.Network.Https.Interfaces
{
    public interface IInventoryGrpcService
    {
        UniTask<GetInventoryResponse> GetInventoryAsync(GetInventoryRequest request, CancellationToken ct = default);

        // Main 싱글 경로: 클라 로컬 드랍/줍기 확정 후 직접 지급 호출(서버 가드 — loot-drop.md §1.4).
        UniTask<GrantItemResponse> GrantItemAsync(GrantItemRequest request, CancellationToken ct = default);
    }
}
