using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Gameplay.Spawn;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.EditMode.Gameplay
{
    /// <summary>
    /// 스폰 데이터의 **출처** 계약 (F6).
    ///
    /// 클라는 MapDefinition(SO)을 Addressables 로 직접 읽는다 — Resources 사본은 없다.
    /// 대신 서버는 여전히 bake(spawn-layouts.json)를 읽으므로, **Export 를 잊으면 클라와 서버의
    /// 스폰 위치가 갈린다.** 그 사고를 막는 것이 아래 드리프트 가드다.
    /// </summary>
    public class SpawnLayoutSourceTests
    {
        private const string ServerJsonRelative = "../../ServerAll/Shared/Shared.Infrastructure/Spawn/spawn-layouts.json";

        private static List<MapDefinition> AllMapDefinitions()
            => AssetDatabase.FindAssets("t:MapDefinition")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<MapDefinition>)
                .Where(d => d != null)
                .OrderBy(d => d.mapId, StringComparer.Ordinal)
                .ToList();

        [Test]
        public void 모든_MapDefinition_의_레이아웃을_Provider_가_읽을_수_있다()
        {
            var defs = AllMapDefinitions();
            Assert.IsNotEmpty(defs, "MapDefinition 에셋이 하나도 없다");

            var provider = new SpawnLayoutProvider();
            foreach (var def in defs)
            {
                // 저작된 맵인데 못 읽으면 = Addressable 미등록. 그 맵은 런타임에 스폰이 죽는다.
                var layout = provider.Get(def.mapId);
                Assert.IsNotNull(layout, $"'{def.mapId}' 레이아웃 로드 실패");
                Assert.AreEqual(def.playerSpawns.Count, layout.Points.Count,
                    $"'{def.mapId}' 플레이어 스폰 수 불일치");
            }
        }

        [Test]
        public void spawn_layouts_사본이_Resources_에_남아있지_않다()
        {
            // Resources 는 빌드에 항상 포함된다 — 맵이 늘수록 커지는 데이터라 여기 두면 안 된다.
            Assert.IsNull(Resources.Load<TextAsset>("spawn-layouts"),
                "Resources/spawn-layouts.json 사본이 아직 남아 있다");
        }

        [Test]
        public void MapDefinition_저작값이_서버_bake_와_일치한다()
        {
            var path = Path.GetFullPath(Path.Combine(Application.dataPath, ServerJsonRelative));
            Assert.IsTrue(File.Exists(path), $"서버 bake 를 찾지 못했다: {path}");

            var baked = JsonUtility.FromJson<FileDto>(File.ReadAllText(path));
            Assert.IsNotNull(baked?.maps, "서버 bake 파싱 실패");

            var defs = AllMapDefinitions();
            var bakedById = baked.maps.ToDictionary(m => m.mapId, m => m);

            Assert.AreEqual(
                defs.Select(d => d.mapId).ToArray(),
                baked.maps.Select(m => m.mapId).OrderBy(x => x, StringComparer.Ordinal).ToArray(),
                "맵 목록이 SO 와 서버 bake 사이에서 갈렸다 — Tools/Spawn/Export Map Data 를 실행하라");

            foreach (var def in defs)
            {
                var m = bakedById[def.mapId];
                var where = $"'{def.mapId}'";

                Assert.AreEqual(def.expReward, m.expReward, $"{where} expReward 불일치");
                Assert.AreEqual(def.monsterLevel, m.monsterLevel, $"{where} monsterLevel 불일치");

                Assert.AreEqual(def.playerSpawns.Count, m.points?.Count ?? 0, $"{where} 스폰 수 불일치");
                for (int i = 0; i < def.playerSpawns.Count; i++)
                {
                    var a = def.playerSpawns[i];
                    var b = m.points[i];
                    Assert.AreEqual(a.position.x, b.x, 0.0001f, $"{where} points[{i}].x");
                    Assert.AreEqual(a.position.y, b.y, 0.0001f, $"{where} points[{i}].y");
                    Assert.AreEqual(a.position.z, b.z, 0.0001f, $"{where} points[{i}].z");
                    Assert.AreEqual(a.rotationY, b.rotY, 0.0001f, $"{where} points[{i}].rotY");
                }

                Assert.AreEqual(def.monsterSpawns.Count, m.monsters?.Count ?? 0, $"{where} 몬스터 수 불일치");
                for (int i = 0; i < def.monsterSpawns.Count; i++)
                {
                    var a = def.monsterSpawns[i];
                    var b = m.monsters[i];
                    Assert.AreEqual(a.monsterId, b.monsterId, $"{where} monsters[{i}].monsterId");
                    Assert.AreEqual(a.position.x, b.x, 0.0001f, $"{where} monsters[{i}].x");
                    Assert.AreEqual(a.position.y, b.y, 0.0001f, $"{where} monsters[{i}].y");
                    Assert.AreEqual(a.position.z, b.z, 0.0001f, $"{where} monsters[{i}].z");
                    Assert.AreEqual(a.slotId, b.slotId, $"{where} monsters[{i}].slotId");
                    Assert.AreEqual(a.respawnCooldownMs, b.respawnCooldownMs, $"{where} monsters[{i}].respawnCooldownMs");
                }
            }
        }

        // 서버 bake JSON 파싱용 DTO (MapDataExporter 가 쓰는 형식과 동일 키).
        [Serializable] private sealed class FileDto { public List<MapDto> maps; }

        [Serializable]
        private sealed class MapDto
        {
            public string mapId;
            public long expReward;
            public int monsterLevel;
            public List<PointDto> points;
            public List<MonsterDto> monsters;
        }

        [Serializable] private sealed class PointDto { public float x, y, z, rotY; }

        [Serializable]
        private sealed class MonsterDto
        {
            public string monsterId;
            public float x, y, z;
            public int slotId;
            public int respawnCooldownMs;
        }
    }
}
