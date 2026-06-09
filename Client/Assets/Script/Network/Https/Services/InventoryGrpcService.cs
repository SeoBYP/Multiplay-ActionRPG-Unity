using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Network.Https.Core;
using Game.Network.Https.Interfaces;
using GameServer.Grpc.Inventory;

namespace Game.Network.Https.Services
{
    /// <summary>
    /// inventory.proto gRPC 호출 래퍼. 공용 채널(GrpcChannelProvider) 공유.
    /// </summary>
    public sealed class InventoryGrpcService : IInventoryGrpcService
    {
        private readonly GrpcChannelProvider _channelProvider;

        public InventoryGrpcService(GrpcChannelProvider channelProvider)
        {
            _channelProvider = channelProvider;
        }

        private InventoryService.InventoryServiceClient CreateClient()
        {
            return new InventoryService.InventoryServiceClient(_channelProvider.CallInvoker);
        }

        public async UniTask<GetInventoryResponse> GetInventoryAsync(GetInventoryRequest request, CancellationToken ct = default)
        {
            var client = CreateClient();
            var call = client.GetInventoryAsync(request, cancellationToken: ct);
            return await call.ResponseAsync;
        }

        public async UniTask<GrantItemResponse> GrantItemAsync(GrantItemRequest request, CancellationToken ct = default)
        {
            var client = CreateClient();
            var call = client.GrantItemAsync(request, cancellationToken: ct);
            return await call.ResponseAsync;
        }
    }
}
