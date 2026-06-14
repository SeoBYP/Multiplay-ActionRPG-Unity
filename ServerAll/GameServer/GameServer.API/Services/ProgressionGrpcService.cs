using GameServer.API.Extension;
using GameServer.API.Extensions;
using GameServer.Application.Common;
using GameServer.Application.Domains.Progression.Interfaces;
using GameServer.Grpc.Progression;
using Grpc.Core;
using Shared.Infrastructure.Progression;
using ProgressionGrpc = GameServer.Grpc.Progression.ProgressionService;

namespace GameServer.API.Services;

/// <summary>
/// 진행(레벨/경험치) + 레벨 파생 스탯 조회 gRPC. 스탯창(7.3) pull 진입점.
/// userId 는 JWT 에서 추출(AuthInterceptor). 스탯은 DB 가 아니라 LevelTable 룩업으로 합성(단일소스).
/// </summary>
public class ProgressionGrpcService(
    IProgressionService progressionService,
    ILogger<ProgressionGrpcService> logger) : ProgressionGrpc.ProgressionServiceBase
{
    public override async Task<GetProgressionResponse> GetProgression(GetProgressionRequest request, ServerCallContext context)
    {
        var userId = context.GetUserId();
        if (userId is null)
        {
            logger.LogWarning("GetProgression rejected because user id was missing");
            return new GetProgressionResponse { Result = ResultExtensions.CreateUnauthorizedGrpcResult() };
        }

        var progression = await progressionService.GetProgressionAsync(userId.Value, context.CancellationToken);
        var stats = LevelTable.StatsAt(progression.Level);
        bool atMax = progression.Level >= LevelTable.MaxLevel;

        var response = new GetProgressionResponse
        {
            Result = Result.Success().ToGrpcResult(),
            Level = progression.Level,
            Exp = progression.Exp,
            ExpToNext = atMax ? 0 : LevelTable.ExpToNext(progression.Level), // 만렙 = 0(다음 없음)
            MaxLevel = LevelTable.MaxLevel,
            Stats = new StatBlock
            {
                MaxHealth = stats.MaxHealth,
                MaxMana = stats.MaxMana,
                AttackPower = stats.AttackPower,
                Defense = stats.Defense,
                Strength = stats.Strength,
                Dexterity = stats.Dexterity,
                Intelligence = stats.Intelligence,
            },
        };

        logger.LogInformation("GetProgression succeeded for user {UserId}: Lv{Level} Exp {Exp}", userId, response.Level, response.Exp);
        return response;
    }
}
