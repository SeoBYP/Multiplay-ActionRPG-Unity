using System.Threading;
using Cysharp.Threading.Tasks;

namespace Game.System.Wallet
{
    /// <summary>
    /// 재화(골드) 서비스. 잔액 조회만(서버 권위). 증감 RPC 는 없다 — 골드 증감은 서버 내부(루트/킬/상점)에서만.
    /// proto(GameServer.Grpc.Wallet)는 여기서 숨기고 도메인 값(long Gold)만 노출.
    /// </summary>
    public interface IWalletService
    {
        UniTask<(WalletResult Result, long Gold)> GetWalletAsync(CancellationToken ct = default);
    }
}
