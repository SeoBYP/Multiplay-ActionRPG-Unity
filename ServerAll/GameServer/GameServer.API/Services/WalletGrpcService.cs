using GameServer.API.Extension;
using GameServer.API.Extensions;
using GameServer.Application.Common;
using GameServer.Application.Domains.Wallet.Interfaces;
using GameServer.Grpc.Wallet;
using Grpc.Core;
using WalletGrpc = GameServer.Grpc.Wallet.WalletService;

namespace GameServer.API.Services;

/// <summary>
/// 조회 전용. 증감은 서버 내부(루트/킬/상점)에서만 일어나며 클라가 증감을 요청하는 RPC 는 두지 않는다(치팅 차단).
/// </summary>
public class WalletGrpcService(
    IWalletService walletService,
    ILogger<WalletGrpcService> logger) : WalletGrpc.WalletServiceBase
{
    public override async Task<GetWalletResponse> GetWallet(GetWalletRequest request, ServerCallContext context)
    {
        var userId = context.GetUserId();
        if (userId is null)
        {
            logger.LogWarning("GetWallet rejected because user id was missing");
            return new GetWalletResponse { Result = ResultExtensions.CreateUnauthorizedGrpcResult() };
        }

        var balance = await walletService.GetBalanceAsync(userId.Value, context.CancellationToken);

        logger.LogInformation("GetWallet succeeded for user {UserId}: balance {Balance}", userId, balance);
        return new GetWalletResponse
        {
            Result = Result.Success().ToGrpcResult(),
            Balance = balance,
        };
    }
}
