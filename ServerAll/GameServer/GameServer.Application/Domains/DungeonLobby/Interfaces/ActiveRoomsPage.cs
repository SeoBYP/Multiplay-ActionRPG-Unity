namespace GameServer.Application.Domains.DungeonLobby.Interfaces;

/// <summary>
/// 활성 방 한 페이지와, 페이지 크기와 무관한 전체 활성 방 수.
/// </summary>
public sealed record ActiveRoomsPage(
    IReadOnlyList<GameServer.Domain.Entities.DungeonRoom> Rooms,
    long TotalCount);
