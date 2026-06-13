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
        public IReadOnlyList<MonsterSlot> Monsters { get; }

        public MapSpawnLayout(string mapId, IReadOnlyList<SpawnPoint> points, IReadOnlyList<MonsterSlot> monsters = null)
        {
            MapId = mapId;
            Points = points;
            Monsters = monsters ?? Array.Empty<MonsterSlot>();
        }
    }

    /// <summary>
    /// 한 맵의 몬스터 슬롯(Main B-lite, 런타임 레이아웃). 클라가 슬롯 기반 로컬 스폰 + 줍기 시 SlotId 로 ClaimKill 클레임.
    /// ※ 서버 Shared.Infrastructure.Spawn.MonsterSpawnDef 의 미러(클레임 관련 필드만). main-spawn-claim.md.
    /// (저작 SO 타입 MapDefinition.MonsterSpawn 과 별개 — 이쪽은 spawn-layouts.json 파싱 결과.)
    /// </summary>
    public sealed class MonsterSlot
    {
        public string MonsterId { get; }
        public float X { get; }
        public float Y { get; }
        public float Z { get; }
        public int SlotId { get; }
        public int RespawnCooldownMs { get; }

        public MonsterSlot(string monsterId, float x, float y, float z, int slotId, int respawnCooldownMs)
        {
            MonsterId = monsterId; X = x; Y = y; Z = z; SlotId = slotId; RespawnCooldownMs = respawnCooldownMs;
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
