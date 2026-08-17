using GameServer.Domain.Entities;

namespace GameServer.Application.Domains.DungeonLobby;

/// <summary>
/// 활성 방 목록 한 페이지(9.6). <paramref name="TotalCount"/> 는 페이지가 아니라 <b>전체</b> 활성 방 수 —
/// 클라 페이저가 "N개 중 M개"를 그릴 수 있어야 한다.
/// </summary>
/// <remarks>
/// 값 튜플이 아니라 클래스인 이유: <c>Result&lt;T&gt;</c> 를 gRPC 결과로 옮기는 <c>ToGrpcResult&lt;T&gt;</c> 가
/// <c>where T : class</c> 라 값 타입을 못 싣는다.
/// </remarks>
public sealed record ActiveRoomPage(IReadOnlyList<DungeonRoom> Rooms, int TotalCount);
