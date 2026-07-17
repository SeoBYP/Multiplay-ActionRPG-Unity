using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Gameplay.Spawn;
using UnityEditor;
using UnityEngine;

namespace Game.Gameplay.Editor
{
    /// <summary>
    /// MapDefinition(SO) ↔ spawn-layouts.json 양방향 툴.
    ///
    /// - Export: 모든 MapDefinition 을 모아 JSON 으로 bake → 클라 Resources + 서버 임베디드 경로 동시 기록.
    ///   (서버는 UnityEngine 의존 0이라 SO를 못 읽으므로 JSON 이 유일한 교환 포맷.)
    ///   ※ 서버는 임베디드 리소스라 export 후 서버 재빌드 시 반영된다.
    /// - Import(부트스트랩): 기존 JSON → MapDefinition 에셋 생성. 손편집 JSON을 SO 저작 체계로 1회 이관.
    ///
    /// JSON 형식은 런타임 로더(SpawnLayoutProvider / SpawnLayoutTable)가 읽는 것과 동일하다:
    ///   { "maps": [ { "mapId": "...", "points": [ { "x":.., "y":.., "z":.., "rotY":.. } ] } ] }
    /// </summary>
    public static class MapDataExporter
    {
        private const string ClientJsonRelative = "Script/Gameplay/Resources/spawn-layouts.json"; // Assets 기준
        private const string ServerJsonRelative = "ServerAll/Shared/Shared.Infrastructure/Spawn/spawn-layouts.json"; // repo 루트 기준
        private const string MapAssetDir = "Assets/GameData/Maps"; // 런타임은 Addressables(address=asset path)로 로드. Resources 밖.

        [MenuItem("Tools/Spawn/Export Map Data (SO → JSON)")]
        public static void Export()
        {
            var count = BakeAll();
            if (count < 0) return; // 검증 실패 — 콘솔에 사유 출력됨
            if (count == 0)
            {
                EditorUtility.DisplayDialog("Export Map Data",
                    "MapDefinition 에셋이 없습니다. 먼저 'Import Map Data from JSON' 으로 부트스트랩하거나 SO를 생성하세요.", "확인");
                return;
            }
            EditorUtility.DisplayDialog("Export Map Data",
                $"맵 {count}개를 클라/서버 JSON 에 기록했습니다.\n서버 반영은 서버 재빌드가 필요합니다.", "확인");
        }

        /// <summary>
        /// 모든 MapDefinition → spawn-layouts.json bake(클라+서버 동시 기록). 다이얼로그 없음.
        /// 반환: 기록한 맵 수 / 0(에셋 없음) / -1(검증 실패, 콘솔에 사유). MapEditorWindow 등에서 재사용.
        /// </summary>
        public static int BakeAll()
        {
            var defs = LoadAllMapDefinitions();
            if (defs.Count == 0)
            {
                Debug.LogWarning("[MapDataExporter] MapDefinition 에셋이 없습니다.");
                return 0;
            }

            // 검증: mapId 중복/공란, 빈 스폰
            var seen = new HashSet<string>();
            foreach (var def in defs)
            {
                if (string.IsNullOrWhiteSpace(def.mapId))
                {
                    Debug.LogError($"[MapDataExporter] mapId 가 비어있는 MapDefinition: {AssetDatabase.GetAssetPath(def)}");
                    return -1;
                }
                if (!seen.Add(def.mapId))
                {
                    Debug.LogError($"[MapDataExporter] mapId 중복: '{def.mapId}'");
                    return -1;
                }
                if (def.playerSpawns == null || def.playerSpawns.Count == 0)
                    Debug.LogWarning($"[MapDataExporter] '{def.mapId}' 의 playerSpawns 가 비어있습니다.");
            }

            var file = new FileDto
            {
                maps = defs
                    .OrderBy(d => d.mapId, StringComparer.Ordinal)
                    .Select(d => new MapDto
                    {
                        mapId = d.mapId,
                        expReward = d.expReward,
                        monsterLevel = d.monsterLevel, // AC-E: 던전 기본 몬스터 레벨(0=L1)
                        bounds = ToBoundsDto(d.bounds),
                        points = (d.playerSpawns ?? new List<MapSpawnPoint>())
                            .Select(p => new PointDto
                            {
                                x = p.position.x, y = p.position.y, z = p.position.z, rotY = p.rotationY
                            })
                            .ToList(),
                        monsters = (d.monsterSpawns ?? new List<MonsterSpawn>())
                            .Select(mn => new MonsterDto
                            {
                                monsterId = mn.monsterId,
                                x = mn.position.x, y = mn.position.y, z = mn.position.z, rotY = mn.rotationY,
                                count = mn.count, wave = mn.wave,
                                level = mn.level, tier = (int)mn.tier, // AC-E: 0=맵 기본 / 등급은 int 계약
                                slotId = mn.slotId, respawnCooldownMs = mn.respawnCooldownMs,
                                patrol = (mn.patrolPoints ?? new List<Vector3>())
                                    .Select(p => new PatrolDto { x = p.x, z = p.z })
                                    .ToList()
                            })
                            .ToList()
                    })
                    .ToList()
            };

            var json = JsonUtility.ToJson(file, true);

            var clientPath = Path.Combine(Application.dataPath, ClientJsonRelative);
            var serverPath = Path.Combine(RepoRoot(), ServerJsonRelative);

            WriteFile(clientPath, json);
            WriteFile(serverPath, json);

            AssetDatabase.Refresh();
            Debug.Log($"[MapDataExporter] Export 완료 — 맵 {file.maps.Count}개\n  클라: {clientPath}\n  서버: {serverPath}\n  ※ 서버 반영은 서버 재빌드 필요.");
            return file.maps.Count;
        }

        [MenuItem("Tools/Spawn/Import Map Data from JSON (bootstrap)")]
        public static void Import()
        {
            var clientPath = Path.Combine(Application.dataPath, ClientJsonRelative);
            var serverPath = Path.Combine(RepoRoot(), ServerJsonRelative);
            var sourcePath = File.Exists(clientPath) ? clientPath
                : File.Exists(serverPath) ? serverPath
                : null;

            if (sourcePath == null)
            {
                EditorUtility.DisplayDialog("Import Map Data", "spawn-layouts.json 을 찾지 못했습니다.", "확인");
                return;
            }

            var file = JsonUtility.FromJson<FileDto>(File.ReadAllText(sourcePath));
            if (file?.maps == null || file.maps.Count == 0)
            {
                EditorUtility.DisplayDialog("Import Map Data", "JSON 파싱 실패 또는 맵이 없습니다.", "확인");
                return;
            }

            EnsureAssetFolder(MapAssetDir);

            int created = 0, updated = 0;
            foreach (var map in file.maps)
            {
                var assetPath = $"{MapAssetDir}/{map.mapId}.asset";
                var def = AssetDatabase.LoadAssetAtPath<MapDefinition>(assetPath);
                bool isNew = def == null;
                if (isNew) def = ScriptableObject.CreateInstance<MapDefinition>();

                def.mapId = map.mapId;
                def.expReward = map.expReward;
                def.monsterLevel = map.monsterLevel; // 왕복 필수 — 빠지면 Import 가 저작한 레벨을 0 으로 지운다
                def.playerSpawns = (map.points ?? new List<PointDto>())
                    .Select(p => new MapSpawnPoint
                    {
                        position = new Vector3(p.x, p.y, p.z),
                        rotationY = p.rotY
                    })
                    .ToList();
                def.monsterSpawns = (map.monsters ?? new List<MonsterDto>())
                    .Select(mn => new MonsterSpawn
                    {
                        monsterId = mn.monsterId,
                        position = new Vector3(mn.x, mn.y, mn.z),
                        rotationY = mn.rotY,
                        count = mn.count,
                        wave = mn.wave,
                        level = mn.level,
                        tier = (MonsterTier)mn.tier, // JSON 계약은 int → 저작 enum 으로 복원
                        slotId = mn.slotId,
                        respawnCooldownMs = mn.respawnCooldownMs,
                        patrolPoints = (mn.patrol ?? new List<PatrolDto>())
                            .Select(p => new Vector3(p.x, 0f, p.z))
                            .ToList()
                    })
                    .ToList();
                def.bounds = map.bounds != null
                    ? new MapBounds { centerX = map.bounds.centerX, centerZ = map.bounds.centerZ, sizeX = map.bounds.sizeX, sizeZ = map.bounds.sizeZ }
                    : new MapBounds();

                if (isNew)
                {
                    AssetDatabase.CreateAsset(def, assetPath);
                    created++;
                }
                else
                {
                    EditorUtility.SetDirty(def);
                    updated++;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[MapDataExporter] Import 완료 — 신규 {created}, 갱신 {updated} ({MapAssetDir})");
            EditorUtility.DisplayDialog("Import Map Data",
                $"부트스트랩 완료 — 신규 {created}, 갱신 {updated}\n위치: {MapAssetDir}", "확인");
        }

        private static List<MapDefinition> LoadAllMapDefinitions()
        {
            return AssetDatabase.FindAssets("t:MapDefinition")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<MapDefinition>)
                .Where(d => d != null)
                .ToList();
        }

        /// <summary>"Assets/A/B" 형태 폴더를 AssetDatabase 기준으로 보장(없으면 단계별 생성).</summary>
        private static void EnsureAssetFolder(string assetFolder)
        {
            if (AssetDatabase.IsValidFolder(assetFolder)) return;

            var parts = assetFolder.Split('/');
            var current = parts[0]; // "Assets"
            for (int i = 1; i < parts.Length; i++)
            {
                var next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static void WriteFile(string path, string content)
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(path, content);
        }

        /// <summary>Application.dataPath = repo/Client/Assets → repo 루트.</summary>
        private static string RepoRoot()
        {
            return Directory.GetParent(Application.dataPath)!.Parent!.FullName;
        }

        private static BoundsDto ToBoundsDto(MapBounds b)
        {
            b ??= new MapBounds();
            return new BoundsDto { centerX = b.centerX, centerZ = b.centerZ, sizeX = b.sizeX, sizeZ = b.sizeZ };
        }

        // ── JSON DTO (런타임 로더와 동일 형식) ──
        [Serializable] private sealed class FileDto { public List<MapDto> maps = new(); }
        [Serializable] private sealed class MapDto { public string mapId; public long expReward; public int monsterLevel; public BoundsDto bounds = new(); public List<PointDto> points = new(); public List<MonsterDto> monsters = new(); }
        [Serializable] private sealed class PointDto { public float x, y, z, rotY; }
        [Serializable] private sealed class MonsterDto { public string monsterId; public float x, y, z, rotY; public int count = 1; public int wave; public int level; public int tier; public int slotId; public int respawnCooldownMs; public List<PatrolDto> patrol = new(); }
        [Serializable] private sealed class PatrolDto { public float x, z; }
        [Serializable] private sealed class BoundsDto { public float centerX, centerZ, sizeX, sizeZ; }
    }
}
