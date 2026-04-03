using GameServer.API.Extension;
using GameServer.API.Extensions;
using GameServer.Application.Domains.User.Interfaces;
using GameServer.Grpc.User;
using Grpc.Core;
using UserGrpc = GameServer.Grpc.User.UserService;

namespace GameServer.API.Services;

public class UserGrpcService(
    IUserProfileService userProfileService,
    ILogger<UserGrpcService> logger) : UserGrpc.UserServiceBase
{
    public override async Task<SetNicknameResponse> SetNickName(SetNicknameRequest request, ServerCallContext context)
    {
        var sessionId = context.GetSessionId();
        if (sessionId is null)
        {
            logger.LogWarning("SetNickName rejected because session id was missing");
            return new SetNicknameResponse
            {
                Result = ResultExtensions.CreateUnauthorizedGrpcResult()
            };
        }

        logger.LogInformation("SetNickName request received for session {SessionId}", sessionId);
        var result = await userProfileService.UpdateProfileAsync(sessionId, request.Nickname, context.CancellationToken);
        if (result.IsSuccess)
        {
            logger.LogInformation("SetNickName succeeded for session {SessionId}", sessionId);
            return new SetNicknameResponse
            {
                Result = result.ToGrpcResult(),
            };
        }

        logger.LogWarning("SetNickName failed for session {SessionId} with code {ErrorCode}", sessionId, result.InternalErrorCode);
        return new SetNicknameResponse { Result = result.ToGrpcResult() };
    }
}
