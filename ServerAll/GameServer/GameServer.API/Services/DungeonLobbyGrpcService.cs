using GameServer.API.Extension;
using GameServer.API.Extensions;
using GameServer.Application.Domains.DungeonLobby;
using GameServer.Application.Domains.DungeonLobby.Interfaces;
using GameServer.Application.Domains.GameSession.Interfaces;
using GameServer.Application.Domains.User.Interfaces;
using GameServer.Domain.Entities;
using GameServer.Grpc.DungeonLobby;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using DungeonLobbyService = GameServer.Grpc.DungeonLobby.DungeonLobbyService;

namespace GameServer.API.Services;

public class DungeonLobbyGrpcService(IDungeonLobbyService dungeonLobbyService,
    IDungeonLobbySubscriptionService subscriptionService,
    IGameSessionRepository gameSessionRepository,
    IUserRepository userRepository,
    IDungeonRoomPlayerRepository dungeonRoomPlayerRepository,
    IRoomReadyStore roomReadyStore,
    IUserProfileRepository userProfileRepository,
    IConnectionMultiplexer connectionMultiplexer,
    ILogger<DungeonLobbyGrpcService> logger) : DungeonLobbyService.DungeonLobbyServiceBase
{
    private static readonly TimeSpan PlayerDataTtl = TimeSpan.FromHours(2);

    public override async Task<CreateRoomResponse> CreateRoom(CreateRoomRequest request, ServerCallContext context)
    {
        var sessionId = context.GetSessionId();
        if (sessionId is null) 
        {
            logger.LogWarning("CreateRoom rejected because session id was missing");
            return new CreateRoomResponse
            {
                Result = ResultExtensions.CreateUnauthorizedGrpcResult(),
                RoomInfo = null,
                CreatedAt = new DateTimeOffset(DateTime.MinValue).ToUnixTimeSeconds(),
            };
        }

        logger.LogInformation("CreateRoom request received for session {SessionId}", sessionId);
        var result = await dungeonLobbyService.CreateDungeonRoomAsync(sessionId, request.RoomName, request.MaxPlayers, request.MapId, context.CancellationToken);
        if (!result.IsSuccess || result.Value is null)
        {
            logger.LogWarning("CreateRoom failed for session {SessionId} with code {ErrorCode}", sessionId, result.InternalErrorCode);
            return new CreateRoomResponse { Result = result.ToGrpcResult() };
        }

        logger.LogInformation("CreateRoom succeeded for session {SessionId} with room {RoomId}", sessionId, result.Value.RoomId);
        return new CreateRoomResponse
        {
            Result = result.ToGrpcResult(),
            RoomInfo = await result.Value.ToRoomInfo(userRepository, dungeonRoomPlayerRepository, roomReadyStore),
            CreatedAt = new DateTimeOffset(result.Value.CreatedAt).ToUnixTimeSeconds(),
        };
    }

    public override async Task<GetRoomResponse> GetRoom(GetRoomRequest request, ServerCallContext context)
    {
        var sessionId = context.GetSessionId();
        if (sessionId is null) 
        {
            logger.LogWarning("GetRoom rejected because session id was missing");
            return new GetRoomResponse
            {
                Result = ResultExtensions.CreateUnauthorizedGrpcResult(),
                RoomInfo = null,
            };
        }

        logger.LogInformation("GetRoom request received for room {RoomId}", request.RoomId);
        var result = await dungeonLobbyService.GetDungeonRoomAsync(request.RoomId, context.CancellationToken);
        if (!result.IsSuccess)
            logger.LogWarning("GetRoom failed for room {RoomId} with code {ErrorCode}", request.RoomId, result.InternalErrorCode);
        return new GetRoomResponse
        {
            Result = result.ToGrpcResult(),
            RoomInfo = result.Value is null ? null : await result.Value.ToRoomInfo(userRepository, dungeonRoomPlayerRepository, roomReadyStore),
        };
    }

    public override async Task<GetRoomsResponse> GetRooms(GetRoomsRequest request, ServerCallContext context)
    {
        var sessionId = context.GetSessionId();
        if (sessionId is null) 
        {
            logger.LogWarning("GetRooms rejected because session id was missing");
            return new GetRoomsResponse
            {
                Result = ResultExtensions.CreateUnauthorizedGrpcResult(),
                RoomInfos = { }
            };
        }

        logger.LogInformation("GetRooms request received for session {SessionId} (offset {Offset}, count {Count})",
            sessionId, request.Offset, request.RoomCount);

        // 페이징(9.6): 이전엔 request.RoomCount 를 무시하고 전체 활성 방을 반환했다.
        // 크기·오프셋 clamp 는 서비스가 한다(서버가 진실원 — 클라 값 그대로 신뢰 금지).
        var result = await dungeonLobbyService.GetActiveDungeonRoomsAsync(
            request.Offset, request.RoomCount, context.CancellationToken);

        var response = new GetRoomsResponse
        {
            Result = result.ToGrpcResult(),
        };

        if (!result.IsSuccess)
            return response;

        var page = result.Value!;
        var rooms = page.Rooms;
        response.TotalCount = page.TotalCount;

        // N+1 회피: 방마다 (플레이어+유저) 2왕복하던 것을 → 플레이어 1쿼리 + 유저 1쿼리로 배치.
        var roomIds = rooms.Select(r => r.RoomId).ToList();
        var allPlayers = await dungeonRoomPlayerRepository.GetPlayersByRoomIdsAsync(roomIds, context.CancellationToken);
        var allUserIds = allPlayers.Select(p => p.UserId).Distinct().ToList();
        var users = await userRepository.GetByIdsAsync(allUserIds, context.CancellationToken);

        var userById = users.ToDictionary(u => u.UserId);
        var playersByRoom = allPlayers
            .GroupBy(p => p.RoomId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // 준비 상태도 함께 싣는다. 목록에서 고른 방이 그대로 대기실 State 로 들어가므로,
        // 여기서 비우면 대기실이 열린 직후 전원 미준비로 잘못 그려진다(방마다 왕복하지 않도록 배치 조회).
        var readyByRoom = await roomReadyStore.GetReadyUserIdsAsync(roomIds, context.CancellationToken);

        foreach (var dungeonRoom in rooms)
        {
            var playerUsers = playersByRoom.TryGetValue(dungeonRoom.RoomId, out var players)
                ? players.Where(p => userById.ContainsKey(p.UserId))
                    .OrderBy(p => p.JoinedAt).ThenBy(p => p.UserId)
                    .Select(p => userById[p.UserId]).ToList()
                : [];

            readyByRoom.TryGetValue(dungeonRoom.RoomId, out var readyUserIds);
            response.RoomInfos.Add(dungeonRoom.ToRoomInfo(playerUsers, readyUserIds));
        }

        logger.LogInformation("GetRooms succeeded for session {SessionId} with {RoomCount} rooms", sessionId, response.RoomInfos.Count);
        return response;
    }

    public override async Task<JoinRoomResponse> JoinRoom(JoinRoomRequest request, ServerCallContext context)
    {
        var sessionId = context.GetSessionId();
        if (sessionId is null) 
        {
            logger.LogWarning("JoinRoom rejected because session id was missing");
            return new JoinRoomResponse
            {
                Result = ResultExtensions.CreateUnauthorizedGrpcResult(),
                RoomInfo = null
            };
        }

        logger.LogInformation("JoinRoom request received for session {SessionId} and room {RoomId}", sessionId, request.RoomId);
        var result = await dungeonLobbyService.JoinRoomAsync(sessionId, request.RoomId, context.CancellationToken);
        if (result.IsSuccess)
            logger.LogInformation("JoinRoom succeeded for session {SessionId} and room {RoomId}", sessionId, request.RoomId);
        else
            logger.LogWarning("JoinRoom failed for session {SessionId} and room {RoomId} with code {ErrorCode}", sessionId, request.RoomId, result.InternalErrorCode);
        return new JoinRoomResponse
        {
            Result = result.ToGrpcResult(),
            RoomInfo = result.Value is null ? null : await result.Value.ToRoomInfo(userRepository, dungeonRoomPlayerRepository, roomReadyStore)
        };
    }

    public override async Task<LeaveRoomResponse> LeaveRoom(LeaveRoomRequest request, ServerCallContext context)
    {
        var sessionId = context.GetSessionId();
        if (sessionId is null) 
        {
            logger.LogWarning("LeaveRoom rejected because session id was missing");
            return new LeaveRoomResponse
            {
                Result = ResultExtensions.CreateUnauthorizedGrpcResult(),
            };
        }

        logger.LogInformation("LeaveRoom request received for session {SessionId} and room {RoomId}", sessionId, request.RoomId);
        var result = await dungeonLobbyService.LeaveRoomAsync(sessionId, request.RoomId, context.CancellationToken);
        if (result.IsSuccess)
            logger.LogInformation("LeaveRoom succeeded for session {SessionId} and room {RoomId}", sessionId, request.RoomId);
        else
            logger.LogWarning("LeaveRoom failed for session {SessionId} and room {RoomId} with code {ErrorCode}", sessionId, request.RoomId, result.InternalErrorCode);
        return new LeaveRoomResponse
        {
            Result = result.ToGrpcResult(),
        };
    }

    public override async Task<StartRoomResponse> StartRoom(StartRoomRequest request, ServerCallContext context)
    {
        var sessionId = context.GetSessionId();
        if (sessionId is null) 
        {
            logger.LogWarning("StartRoom rejected because session id was missing");
            return new StartRoomResponse
            {
                Result = ResultExtensions.CreateUnauthorizedGrpcResult(),
                RoomInfo = null
            };
        }
        var traceId = context.GetHttpContext().Items["TraceId"] as string ?? "";
        logger.LogInformation("StartRoom request received for session {SessionId}, room {RoomId}, trace {TraceId}", sessionId, request.RoomId, traceId);
        var result = await dungeonLobbyService.StartGameAsync(sessionId, request.RoomId, traceId, request.MapId, context.CancellationToken);
        if (result.IsSuccess)
            logger.LogInformation("StartRoom succeeded for session {SessionId} and room {RoomId}", sessionId, request.RoomId);
        else
            logger.LogWarning("StartRoom failed for session {SessionId} and room {RoomId} with code {ErrorCode}", sessionId, request.RoomId, result.InternalErrorCode);
        return new StartRoomResponse
        {
            Result = result.ToGrpcResult(),
            RoomInfo = result.Value is null ? null : await result.Value.ToRoomInfo(userRepository, dungeonRoomPlayerRepository, roomReadyStore),
        };
    }

    public override async Task<SetReadyResponse> SetReady(SetReadyRequest request, ServerCallContext context)
    {
        var sessionId = context.GetSessionId();
        if (sessionId is null)
        {
            logger.LogWarning("SetReady rejected because session id was missing");
            return new SetReadyResponse { Result = ResultExtensions.CreateUnauthorizedGrpcResult() };
        }

        var result = await dungeonLobbyService.SetReadyAsync(
            sessionId, request.RoomId, request.IsReady, context.CancellationToken);

        if (!result.IsSuccess)
            logger.LogWarning("SetReady failed for room {RoomId} with code {ErrorCode}", request.RoomId, result.InternalErrorCode);

        return new SetReadyResponse
        {
            Result = result.ToGrpcResult(),
            RoomInfo = result.Value is null
                ? null
                : await result.Value.ToRoomInfo(userRepository, dungeonRoomPlayerRepository, roomReadyStore),
        };
    }

    public override async Task<UpdateRoomResponse> UpdateRoom(UpdateRoomRequest request, ServerCallContext context)
    {
        var sessionId = context.GetSessionId();
        if (sessionId is null) 
        {
            logger.LogWarning("UpdateRoom rejected because session id was missing");
            return new UpdateRoomResponse
            {
                Result = ResultExtensions.CreateUnauthorizedGrpcResult(),
                RoomInfo = null
            };
        }

        logger.LogInformation("UpdateRoom request received for session {SessionId} and room {RoomId}", sessionId, request.RoomId);
        var result = await dungeonLobbyService.UpdateRoomSettingsAsync(sessionId, request.RoomId, request.RoomName, request.MaxPlayers, context.CancellationToken);
        if (result.IsSuccess)
            logger.LogInformation("UpdateRoom succeeded for session {SessionId} and room {RoomId}", sessionId, request.RoomId);
        else
            logger.LogWarning("UpdateRoom failed for session {SessionId} and room {RoomId} with code {ErrorCode}", sessionId, request.RoomId, result.InternalErrorCode);
        return new UpdateRoomResponse
        {
            Result = result.ToGrpcResult(),
            RoomInfo = result.Value is null ? null : await result.Value.ToRoomInfo(userRepository, dungeonRoomPlayerRepository, roomReadyStore)
        };
    }

    public override async Task SubscribeRoom(SubscribeRoomRequest request,
        IServerStreamWriter<SubscribeRoomResponse> responseStream,
        ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var sessionId = context.GetSessionId();
        if (sessionId is null) 
            throw new InvalidOperationException("Session ID cannot be null");
        logger.LogInformation("SubscribeRoom started for session {SessionId} and room {RoomId}", sessionId, request.RoomId);
        
        var validation = await dungeonLobbyService.ValidateSubscriptionAsync(sessionId, request.RoomId, ct);
        if (!validation.IsSuccess)
        {
            logger.LogWarning("SubscribeRoom rejected for session {SessionId}: {ErrorCode}", sessionId, validation.InternalErrorCode);
            return;
        }

        var ctx = await subscriptionService.SubscribeAsync(validation.Value, request.RoomId, ct);

        // 방이 Waiting이 아닌 상태(Starting/Playing)로 구독을 시작하는 경우
        // Redis stream에 이벤트가 없어도 현재 상태를 즉시 클라이언트에 전달한다.
        // (서버 재시작 후 재접속, gRPC 끊김 후 복구 등 edge case 처리)
        var currentRoom = await dungeonLobbyService.GetDungeonRoomAsync(request.RoomId, ct);
        if (currentRoom.IsSuccess && currentRoom.Value?.Status != RoomStatus.Waiting)
        {
            ctx.Outbound.Writer.TryWrite(request.RoomId);
            logger.LogInformation("SubscribeRoom initial kick for room {RoomId} status {Status}",
                request.RoomId, currentRoom.Value?.Status);

            // Starting 또는 Playing 상태로 재접속한 경우 호스트가 게임 시작 흐름을 자동으로 재트리거한다.
            // - Starting: 이전 GameStartRequestedMessage가 유실됐을 수 있음
            // - Playing: 서버 재시작으로 Redis player key가 소실됐을 수 있음 (StartGameAsync가 idempotent)
            if ((currentRoom.Value?.Status == RoomStatus.Starting ||
                 currentRoom.Value?.Status == RoomStatus.Playing) &&
                currentRoom.Value.HostUserId == validation.Value)
            {
                logger.LogInformation(
                    "SubscribeRoom: host {UserId} reconnected to {Status} room {RoomId}, auto-retriggering StartGame",
                    validation.Value, currentRoom.Value.Status, request.RoomId);
                var retriggerResult = await dungeonLobbyService.StartGameAsync(
                    sessionId, request.RoomId, Guid.NewGuid().ToString(), mapId: "", ct);
                if (!retriggerResult.IsSuccess)
                    logger.LogWarning(
                        "SubscribeRoom auto-retrigger failed for room {RoomId}: {ErrorCode}",
                        request.RoomId, retriggerResult.InternalErrorCode);
            }
        }

        try
        {
            await SendLoopAsync(responseStream, ctx, ct);
        }
        finally
        {
            await subscriptionService.UnsubscribeAsync(ctx, ct);
            logger.LogInformation("SubscribeRoom ended for user {UserId} and room {RoomId}", ctx.UserId, ctx.RoomId);
        }
    }

    private async Task SendLoopAsync(
        IServerStreamWriter<SubscribeRoomResponse> responseStream,
        UserRoomContext ctx,
        CancellationToken ct)
    {
        await foreach (var roomId in ctx.Outbound.Reader.ReadAllAsync(ct))
        {
            var room = await dungeonLobbyService.GetDungeonRoomAsync(roomId, ct);
            if (room.IsSuccess == false)
            {
                logger.LogWarning("[SendLoop] user={UserId} room={RoomId} — GetDungeonRoom 실패: {Error}",
                    ctx.UserId, roomId, room.InternalErrorCode);
                continue;
            }
            if (room.Value is null)
            {
                logger.LogWarning("[SendLoop] user={UserId} room={RoomId} — room.Value null", ctx.UserId, roomId);
                continue;
            }

            logger.LogInformation("[SendLoop] user={UserId} room={RoomId} status={Status} — 이벤트 처리 시작",
                ctx.UserId, roomId, room.Value.Status);

            var serverMsg = new SubscribeRoomResponse();
            switch (room.Value.Status)
            {
                case RoomStatus.Playing:
                    var gameSession = await gameSessionRepository.GetByRoomIdAsync(room.Value.RoomId, ct);
                    if (gameSession is null)
                    {
                        logger.LogWarning("[SendLoop] user={UserId} room={RoomId} — Playing 상태인데 GameSession null, 이벤트 드롭",
                            ctx.UserId, roomId);
                        continue;
                    }

                    logger.LogInformation("[SendLoop] user={UserId} room={RoomId} — GameSessionEvent 전송 (ip={Ip} port={Port})",
                        ctx.UserId, roomId, gameSession.SocketIp, gameSession.SocketPort);

                    await EnsurePlayerDataInRedisAsync(room.Value.RoomId, gameSession.GameSessionId, ct);

                    serverMsg.GameSessionEvent = new GameSessionReadyEvent
                    {
                        Ip = gameSession.SocketIp,
                        Port = gameSession.SocketPort
                    };
                    break;

                default:
                    logger.LogInformation("[SendLoop] user={UserId} room={RoomId} status={Status} — UpdateEvent 전송",
                        ctx.UserId, roomId, room.Value.Status);
                    serverMsg.UpdateEvent = new RoomUpdatedEvent
                    {
                        RoomInfo = await room.Value.ToRoomInfo(userRepository, dungeonRoomPlayerRepository, roomReadyStore),
                    };
                    break;
            }

            await responseStream.WriteAsync(serverMsg, ct);
        }
        logger.LogInformation("[SendLoop] user={UserId} room={RoomId} — 루프 종료", ctx.UserId, ctx.RoomId);
    }

    /// <summary>
    /// Redis player key가 존재하지 않으면 DB에서 조회해 복구한다.
    /// 서버 재시작 후 Playing 방에 재접속할 때 SocketServer 검증에 필요한 키를 보장한다.
    /// </summary>
    private async Task EnsurePlayerDataInRedisAsync(long roomId, long gameSessionId, CancellationToken ct)
    {
        var redis = connectionMultiplexer.GetDatabase();
        var players = await dungeonRoomPlayerRepository.GetPlayersByRoomIdAsync(roomId, ct);

        for (var i = 0; i < players.Count; i++)
        {
            var player = players[i];
            var key = $"gamesession:player:{player.UserId}";

            if (await redis.KeyExistsAsync(key))
                continue;

            var profile = await userProfileRepository.GetByIdAsync(player.UserId, ct);
            var nickname = profile?.NickName ?? $"Player_{player.UserId}";

            var entries = new HashEntry[]
            {
                new("roomId", roomId),
                new("gameSessionId", gameSessionId),
                new("nickname", nickname),
                new("spawnIndex", i)
            };

            await redis.HashSetAsync(key, entries);
            await redis.KeyExpireAsync(key, PlayerDataTtl);

            logger.LogInformation(
                "EnsurePlayerData: restored Redis key for user {UserId} in room {RoomId}",
                player.UserId, roomId);
        }
    }
}
