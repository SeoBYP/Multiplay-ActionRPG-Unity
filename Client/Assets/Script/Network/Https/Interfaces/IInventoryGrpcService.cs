using System.Threading;
using Cysharp.Threading.Tasks;
using GameServer.Grpc.Inventory;

namespace Game.Network.Https.Interfaces
{
    public interface IInventoryGrpcService
    {
        UniTask<GetInventoryResponse> GetInventoryAsync(GetInventoryRequest request, CancellationToken ct = default);
    }
}
