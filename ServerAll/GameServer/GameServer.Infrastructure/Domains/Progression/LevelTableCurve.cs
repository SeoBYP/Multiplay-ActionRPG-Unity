using GameServer.Domain.Entities.User;
using Shared.Infrastructure.Progression;

namespace GameServer.Infrastructure.Domains.Progression;

/// <summary>
/// <see cref="ILevelCurve"/> 의 실데이터 구현 — Shared.Infrastructure <see cref="LevelTable"/>(임베디드 JSON)에 위임.
/// 무상태(정적 테이블 조회만)라 단일 인스턴스를 공유한다.
/// </summary>
public sealed class LevelTableCurve : ILevelCurve
{
    public static readonly LevelTableCurve Instance = new();

    private LevelTableCurve() { }

    public int MaxLevel => LevelTable.MaxLevel;

    public long ExpToNext(int level) => LevelTable.ExpToNext(level);
}
