using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Network.Https.Interfaces;
using GameServer.Grpc.Wallet;
using UnityEngine;

namespace Game.System.Wallet
{
    /// <summary>
    /// 재화 서비스. gRPC(WalletService.GetWallet) 호출 → 도메인 값(long Gold) 변환.
    /// InventoryService 와 동형(에러 catch → Failed, 0).
    /// </summary>
    public sealed class WalletService : IWalletService
    {
        private readonly IWalletGrpcService _grpc;

        public WalletService(IWalletGrpcService grpc)
        {
            _grpc = grpc;
        }

        public async UniTask<(WalletResult Result, long Gold)> GetWalletAsync(CancellationToken ct = default)
        {
            try
            {
                var res = await _grpc.GetWalletAsync(new GetWalletRequest(), ct);
                if (!res.Result.Success)
                {
                    Debug.LogWarning($"[WalletService] GetWallet 실패: code={res.Result.ErrorCode}");
                    return (WalletResult.Failed, 0);
                }

                return (WalletResult.Success, res.Balance);
            }
            catch (Exception e)
            {
                Debug.LogError($"[WalletService] GetWallet 예외: {e.Message}");
                return (WalletResult.Failed, 0);
            }
        }
    }
}
