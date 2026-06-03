using System;
using System.Collections.Generic;

namespace Game.Gameplay.Spawn
{
    /// <summary>
    /// 맵 내 한 스폰 지점(위치 + Y축 회전).
    /// ※ 서버 Shared.Infrastructure.Spawn.SpawnPoint 의 미러. 동일하게 유지할 것.
    /// </summary>
    public sealed class SpawnPoint
    {
        public float X { get; }
        public float Y { get; }
        public float Z { get; }
        public float RotY { get; }

        public SpawnPoint(float x, float y, float z, float rotY)
        {
            X = x; Y = y; Z = z; RotY = rotY;
        }
    }

    /// <summary>한 맵의 스폰 레이아웃 — 명시적 스폰 포인트 목록. spawnIndex = Points 인덱스.</summary>
    public sealed class MapSpawnLayout
    {
        public string MapId { get; }
        public IReadOnlyList<SpawnPoint> Points { get; }

        public MapSpawnLayout(string mapId, IReadOnlyList<SpawnPoint> points)
        {
            MapId = mapId;
            Points = points;
        }
    }

    /// <summary>
    /// 결정론적 스폰 리졸버 — 순수 함수.
    /// ※ 서버 Shared.Infrastructure.Spawn.SpawnResolver.Resolve 와 동일 알고리즘이어야 한다.
    /// 같은 (layout, spawnIndex) 입력 → 항상 같은 SpawnPoint. 그래서 서버·클라가 좌표 전송 없이 일치한다.
    /// </summary>
    public static class SpawnResolver
    {
        public static SpawnPoint Resolve(MapSpawnLayout layout, int spawnIndex)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            if (layout.Points.Count == 0)
                throw new InvalidOperationException($"Spawn layout '{layout.MapId}' has no points.");

            var count = layout.Points.Count;
            var index = ((spawnIndex % count) + count) % count;
            return layout.Points[index];
        }
    }
}
