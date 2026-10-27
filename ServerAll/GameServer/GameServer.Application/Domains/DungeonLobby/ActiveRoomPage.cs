using GameServer.Domain.Entities;

namespace GameServer.Application.Domains.DungeonLobby;

/// <summary>
/// 활성 방 목록 한 페이지. <paramref name="TotalCount"/> 는 페이지가 아니라 <b>전체</b> 활성 방 수.
/// </summary>
/// <remarks>
/// 값 튜플이 아닌 이유: <c>ToGrpcResult&lt;T&gt;</c> 가 <c>where T : class</c> 라 값 타입을 못 싣는다.
/// </remarks>
public sealed record ActiveRoomPage(IReadOnlyList<DungeonRoom> Rooms, int TotalCount);
