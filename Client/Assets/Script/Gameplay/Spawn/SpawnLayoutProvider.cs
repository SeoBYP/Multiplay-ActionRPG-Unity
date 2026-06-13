using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Gameplay.Spawn
{
    /// <summary>
    /// Resources 의 spawn-layouts.json(서버 정본의 미러)을 1회 로드해 맵별 레이아웃을 제공한다.
    /// 서버는 같은 JSON 을 임베디드 리소스로 읽는다 → 양쪽이 동일 데이터.
    /// </summary>
    public sealed class SpawnLayoutProvider
    {
        private const string ResourcePath = "spawn-layouts"; // Resources/spawn-layouts.json

        private readonly Dictionary<string, MapSpawnLayout> _layouts;

        public SpawnLayoutProvider()
        {
            _layouts = Load();
        }

        /// <summary>mapId 의 레이아웃을 반환한다. 알 수 없는 mapId 면 예외(데이터 누락을 조용히 넘기지 않는다).</summary>
        public MapSpawnLayout Get(string mapId)
        {
            if (_layouts.TryGetValue(mapId, out var layout))
                return layout;

            throw new KeyNotFoundException($"Spawn layout for mapId '{mapId}' not found in spawn-layouts.json");
        }

        private static Dictionary<string, MapSpawnLayout> Load()
        {
            var asset = Resources.Load<TextAsset>(ResourcePath);
            if (asset == null)
                throw new InvalidOperationException($"Resources/{ResourcePath}.json not found.");

            var file = JsonUtility.FromJson<SpawnLayoutFileDto>(asset.text);
            if (file?.maps == null)
                throw new InvalidOperationException("Failed to parse spawn-layouts.json");

            var result = new Dictionary<string, MapSpawnLayout>();
            foreach (var map in file.maps)
            {
                var points = new List<SpawnPoint>(map.points.Length);
                foreach (var p in map.points)
                    points.Add(new SpawnPoint(p.x, p.y, p.z, p.rotY));

                var monsters = new List<MonsterSlot>();
                if (map.monsters != null)
                    foreach (var m in map.monsters)
                        monsters.Add(new MonsterSlot(m.monsterId, m.x, m.y, m.z, m.slotId, m.respawnCooldownMs));

                result[map.mapId] = new MapSpawnLayout(map.mapId, points, monsters);
            }
            return result;
        }

        // JsonUtility 직렬화용 DTO (서버 JSON 형식과 동일 키).
        [Serializable]
        private sealed class SpawnLayoutFileDto
        {
            public MapEntryDto[] maps;
        }

        [Serializable]
        private sealed class MapEntryDto
        {
            public string mapId;
            public PointDto[] points;
            public MonsterDto[] monsters;
        }

        [Serializable]
        private sealed class PointDto
        {
            public float x;
            public float y;
            public float z;
            public float rotY;
        }

        // Main 슬롯 기반 스폰 + ClaimKill 클레임에 필요한 필드만(서버 monsters[] 의 부분집합).
        [Serializable]
        private sealed class MonsterDto
        {
            public string monsterId;
            public float x;
            public float y;
            public float z;
            public int slotId;
            public int respawnCooldownMs;
        }
    }
}
