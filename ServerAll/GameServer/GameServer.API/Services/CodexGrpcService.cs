using GameServer.API.Extension;
using GameServer.API.Extensions;
using GameServer.Application.Common;
using GameServer.Application.Domains.Codex.Interfaces;
using GameServer.Grpc.Codex;
using Grpc.Core;
using CodexGrpc = GameServer.Grpc.Codex.CodexService;

namespace GameServer.API.Services;

/// <summary>
/// 도감 gRPC — 조회 전용. 발견 기록(진실원=서버)은 아이템 지급 funnel 에서만 일어나며
/// 클라가 임의 발견을 요청하는 RPC 는 두지 않는다(치팅 차단). 3.7 도감.
/// </summary>
public class CodexGrpcService(
    ICodexService codexService,
    ILogger<CodexGrpcService> logger) : CodexGrpc.CodexServiceBase
{
    public override async Task<GetCodexResponse> GetCodex(GetCodexRequest request, ServerCallContext context)
    {
        var userId = context.GetUserId();
        if (userId is null)
        {
            logger.LogWarning("GetCodex rejected because user id was missing");
            return new GetCodexResponse { Result = ResultExtensions.CreateUnauthorizedGrpcResult() };
        }

        var discovered = await codexService.GetDiscoveredAsync(userId.Value, context.CancellationToken);

        var response = new GetCodexResponse { Result = Result.Success().ToGrpcResult() };
        response.DiscoveredItemIds.AddRange(discovered);

        logger.LogInformation("GetCodex succeeded for user {UserId}: {Count} discovered", userId, discovered.Count);
        return response;
    }
}
