using GameServer.API.Extension;
using GameServer.API.Extensions;
using GameServer.Application.Common;
using GameServer.Application.Domains.User.Interfaces;
using GameServer.Grpc.User;
using Grpc.Core;
using UserGrpc = GameServer.Grpc.User.UserService;

namespace GameServer.API.Services;

public class UserGrpcService(
    IUserProfileService userProfileService,
    IUserPositionService userPositionService,
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
    /// <summary>
    /// Main 위치 보고(B7). 주기 호출 경로라 Redis 에만 쓴다 — 확정(DB)은 이탈 시점에 서버가 한다.
    ///
    /// ⚠ 좌표는 클라가 만든 값이다. 서버는 맵 경계만 검증하고 밖이면 저작 스폰으로 스냅한다.
    /// 근접·궤적 검증은 하지 않는다(cleanup-backlog B7 — 클라가 보고한 좌표로 클라를 검증하는 것은 순환).
    /// </summary>
    public override async Task<SavePositionResponse> SavePosition(SavePositionRequest request, ServerCallContext context)
    {
        var userId = context.GetUserId();
        if (userId is null)
        {
            logger.LogWarning("SavePosition rejected because user id was missing");
            return new SavePositionResponse { Result = ResultExtensions.CreateUnauthorizedGrpcResult() };
        }

        var p = request.Position;
        if (p is null)
            return new SavePositionResponse { Result = Result.Failure(ErrorCodes.InvalidRequest, "position required").ToGrpcResult() };

        var saved = await userPositionService.SaveAsync(
            userId.Value, p.MapId, p.X, p.Y, p.Z, p.RotY, context.CancellationToken);

        if (!saved.Accepted)
            return new SavePositionResponse { Result = Result.Failure(ErrorCodes.InvalidRequest, "position rejected").ToGrpcResult() };

        return new SavePositionResponse { Result = Result.Success().ToGrpcResult() };
    }

    /// <summary>마지막 위치 조회. 없으면 has_position=false — 클라는 저작 스폰으로 폴백한다.</summary>
    public override async Task<GetLastPositionResponse> GetLastPosition(GetLastPositionRequest request, ServerCallContext context)
    {
        var userId = context.GetUserId();
        if (userId is null)
        {
            logger.LogWarning("GetLastPosition rejected because user id was missing");
            return new GetLastPositionResponse { Result = ResultExtensions.CreateUnauthorizedGrpcResult() };
        }

        var pos = await userPositionService.GetAsync(userId.Value, context.CancellationToken);
        if (pos is null)
            return new GetLastPositionResponse { Result = Result.Success().ToGrpcResult(), HasPosition = false };

        return new GetLastPositionResponse
        {
            Result = Result.Success().ToGrpcResult(),
            HasPosition = true,
            Position = new Position
            {
                MapId = pos.MapId, X = pos.X, Y = pos.Y, Z = pos.Z, RotY = pos.RotY,
            },
        };
    }
}
