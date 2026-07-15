using System.Collections.Generic;
using System.Linq;
using Game.Gameplay.Spawn;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Gameplay.Editor
{
    /// <summary>
    /// 맵 저작 윈도우 — 프리뷰 씬에서 맵 비주얼 위에 스폰 마커를 배치하고 MapDefinition(SO)에 write-back + JSON export.
    ///
    /// 워크플로우:
    ///   1) New Editor Scene (선택) → 깨끗한 작업 씬(Camera+Light)
    ///   2) MapDefinition 선택 → Load to Scene (visualPrefab + 기존 스폰 마커 생성)
    ///   3) 마커를 드래그로 배치 / Add Spawn Point
    ///   4) Save to SO & Export JSON → SO 갱신 + 클라/서버 spawn-layouts.json bake
    ///
    /// 'Spawns' 부모 아래 마커 sibling 순서 = SpawnIndex(0,1,2…).
    /// </summary>
    public sealed class MapEditorWindow : EditorWindow
    {
        private const string RootName     = "[MapAuthoring]";
        private const string SpawnsName   = "Spawns";
        private const string MonstersName = "Monsters";
        private const string BoundsName   = "Bounds";

        [SerializeField] private MapDefinition _target;

        [MenuItem("Tools/Spawn/Map Editor Window")]
        public static void Open() => GetWindow<MapEditorWindow>("Map Editor");

        private void OnGUI()
        {
            EditorGUILayout.LabelField("맵 스폰 저작", EditorStyles.boldLabel);
            _target = (MapDefinition)EditorGUILayout.ObjectField("Map Definition", _target, typeof(MapDefinition), false);

            EditorGUILayout.Space();
            if (GUILayout.Button("New Editor Scene (Camera+Light)")) NewEditorScene();

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(_target == null))
            {
                if (GUILayout.Button("1) Load to Scene"))           LoadToScene();
                if (GUILayout.Button("＋ Add Spawn Point"))          AddSpawnPoint();
                if (GUILayout.Button("＋ Add Monster"))              AddMonster();
                if (GUILayout.Button("＋ Add Patrol Point (선택 몬스터)")) AddPatrolPoint();
                if (GUILayout.Button("2) Save to SO & Export JSON")) SaveAndExport();
            }

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "1) MapDefinition 선택 → Load to Scene\n" +
                "2) 스폰(파란 구) · 몬스터(빨간 구) 드래그 배치. Add Monster 후 그 몬스터 선택 → Add Patrol Point 로 경로(주황) 추가\n" +
                "3) 경계(초록 박스 'Bounds') 위치·크기 조정 — 몬스터가 이 박스를 못 벗어남\n" +
                "4) Save to SO & Export → SO + 클라/서버 JSON 갱신 (서버는 재빌드 시 반영)\n" +
                "순서: 'Spawns'=SpawnIndex · 몬스터 아래 'Patrol_N'=패트롤 순회 순서.",
                MessageType.Info);
        }

        private void NewEditorScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        }

        private void LoadToScene()
        {
            var existing = GameObject.Find(RootName);
            if (existing != null) Object.DestroyImmediate(existing);

            var root = new GameObject(RootName);

            if (_target.visualPrefab != null)
            {
                var vis = (GameObject)PrefabUtility.InstantiatePrefab(_target.visualPrefab);
                vis.name = "Visual";
                vis.transform.SetParent(root.transform, true);
            }

            var spawns = new GameObject(SpawnsName);
            spawns.transform.SetParent(root.transform, false);

            var pts = _target.playerSpawns ?? new List<MapSpawnPoint>();
            for (int i = 0; i < pts.Count; i++)
                CreateMarker(spawns.transform, pts[i].position, pts[i].rotationY, i);

            var monsters = new GameObject(MonstersName);
            monsters.transform.SetParent(root.transform, false);
            var mss = _target.monsterSpawns ?? new List<MonsterSpawn>();
            for (int i = 0; i < mss.Count; i++)
                CreateMonsterMarker(monsters.transform, mss[i], i);

            CreateBoundsMarker(root.transform, _target.bounds);

            Selection.activeGameObject = root;
            Debug.Log($"[MapEditorWindow] '{_target.mapId}' 로드 — 스폰 {pts.Count} · 몬스터 {mss.Count}개");
        }

        private void AddSpawnPoint()
        {
            var spawns = FindSpawns(createIfMissing: true);
            int i = spawns.childCount;
            var pos = new Vector3(i, 0f, 0f); // 겹침 방지용 기본 위치
            var marker = CreateMarker(spawns, pos, 0f, i);
            Selection.activeGameObject = marker;
        }

        private void SaveAndExport()
        {
            var spawns = FindSpawns(createIfMissing: false);
            if (spawns == null)
            {
                EditorUtility.DisplayDialog("Save", "씬에 '[MapAuthoring]/Spawns' 가 없습니다. 먼저 Load to Scene 하세요.", "확인");
                return;
            }

            var markers = new List<Transform>();
            foreach (Transform child in spawns) // sibling 순서 = SpawnIndex
                if (child.GetComponent<SpawnPointMarker>() != null) markers.Add(child);

            _target.playerSpawns = markers
                .Select(t => new MapSpawnPoint { position = t.position, rotationY = t.eulerAngles.y })
                .ToList();

            _target.monsterSpawns = ReadMonsterSpawns();

            var boundsMarker = FindBoundsMarker();
            if (boundsMarker != null)
                _target.bounds = new MapBounds
                {
                    centerX = boundsMarker.transform.position.x,
                    centerZ = boundsMarker.transform.position.z,
                    sizeX   = boundsMarker.sizeX,
                    sizeZ   = boundsMarker.sizeZ,
                };

            EditorUtility.SetDirty(_target);
            AssetDatabase.SaveAssets();

            var count = MapDataExporter.BakeAll();
            int monsterCount = _target.monsterSpawns?.Count ?? 0;
            Debug.Log($"[MapEditorWindow] '{_target.mapId}' 저장 — 스폰 {markers.Count} · 몬스터 {monsterCount}개 → SO + JSON(맵 {count}개) export 완료.");
            EditorUtility.DisplayDialog("Save & Export",
                $"'{_target.mapId}' 스폰 {markers.Count} · 몬스터 {monsterCount}개를 SO에 저장하고 JSON으로 export 했습니다.\n서버 반영은 서버 재빌드 필요.", "확인");
        }

        private static GameObject CreateMarker(Transform parent, Vector3 position, float rotationY, int index)
        {
            var go = new GameObject($"Spawn_{index}");
            go.transform.SetParent(parent, false);
            go.transform.position    = position;
            go.transform.eulerAngles = new Vector3(0f, rotationY, 0f);
            go.AddComponent<SpawnPointMarker>();
            return go;
        }

        private static Transform FindSpawns(bool createIfMissing) => FindGroup(SpawnsName, createIfMissing);

        private static Transform FindGroup(string name, bool createIfMissing)
        {
            var root = GameObject.Find(RootName);
            if (root == null)
            {
                if (!createIfMissing) return null;
                root = new GameObject(RootName);
            }
            var group = root.transform.Find(name);
            if (group == null && createIfMissing)
            {
                var go = new GameObject(name);
                go.transform.SetParent(root.transform, false);
                group = go.transform;
            }
            return group;
        }

        // ── 몬스터 / 패트롤 ──────────────────────────────

        private void AddMonster()
        {
            var monsters = FindGroup(MonstersName, createIfMissing: true);
            int i = monsters.childCount;
            var def = new MonsterSpawn
            {
                monsterId = "creepy_demon",
                position  = new Vector3(i, 0f, 3f), // 겹침 방지 기본 위치
                rotationY = 0f,
                count     = 1,
            };
            var go = CreateMonsterMarker(monsters, def, i);
            Selection.activeGameObject = go;
        }

        private void AddPatrolPoint()
        {
            var sel = Selection.activeGameObject;
            Transform monster = sel == null ? null
                : sel.GetComponent<MonsterSpawnMarker>() != null ? sel.transform
                : sel.GetComponent<PatrolPointMarker>() != null ? sel.transform.parent
                : null;

            if (monster == null)
            {
                EditorUtility.DisplayDialog("Add Patrol Point",
                    "몬스터 마커(빨간 구) 또는 그 패트롤 점을 먼저 선택하세요.", "확인");
                return;
            }

            int i = monster.childCount;
            var pos = monster.position + new Vector3(1f + i, 0f, 0f);
            var go = CreatePatrolMarker(monster, pos, i);
            Selection.activeGameObject = go;
        }

        private static List<MonsterSpawn> ReadMonsterSpawns()
        {
            var group = FindGroup(MonstersName, createIfMissing: false);
            var result = new List<MonsterSpawn>();
            if (group == null) return result;

            foreach (Transform m in group)
            {
                var marker = m.GetComponent<MonsterSpawnMarker>();
                if (marker == null) continue;

                var patrol = new List<Vector3>();
                foreach (Transform p in m) // sibling 순서 = 패트롤 순회 순서
                    if (p.GetComponent<PatrolPointMarker>() != null) patrol.Add(p.position);

                result.Add(new MonsterSpawn
                {
                    monsterId         = marker.monsterId,
                    position          = m.position,
                    rotationY         = m.eulerAngles.y,
                    count             = Mathf.Max(1, marker.count),
                    wave              = marker.wave,
                    slotId            = marker.slotId,
                    respawnCooldownMs = marker.respawnCooldownMs,
                    patrolPoints      = patrol,
                });
            }
            return result;
        }

        private static GameObject CreateMonsterMarker(Transform parent, MonsterSpawn def, int index)
        {
            var go = new GameObject($"Monster_{index}");
            go.transform.SetParent(parent, false);
            go.transform.position    = def.position;
            go.transform.eulerAngles = new Vector3(0f, def.rotationY, 0f);

            var m = go.AddComponent<MonsterSpawnMarker>();
            m.monsterId         = string.IsNullOrEmpty(def.monsterId) ? "creepy_demon" : def.monsterId;
            m.count             = Mathf.Max(1, def.count);
            m.wave              = def.wave;
            m.slotId            = def.slotId;
            m.respawnCooldownMs = def.respawnCooldownMs;

            var patrol = def.patrolPoints ?? new List<Vector3>();
            for (int i = 0; i < patrol.Count; i++)
                CreatePatrolMarker(go.transform, patrol[i], i);

            return go;
        }

        private static GameObject CreatePatrolMarker(Transform parent, Vector3 worldPos, int index)
        {
            var go = new GameObject($"Patrol_{index}");
            go.transform.SetParent(parent, false);
            go.transform.position = worldPos;
            go.AddComponent<PatrolPointMarker>();
            return go;
        }

        // ── 경계 ────────────────────────────────────────

        private static void CreateBoundsMarker(Transform root, MapBounds b)
        {
            b ??= new MapBounds();
            var go = new GameObject(BoundsName);
            go.transform.SetParent(root, false);
            go.transform.position = new Vector3(b.centerX, 0f, b.centerZ);

            var m = go.AddComponent<MapBoundsMarker>();
            m.sizeX = b.sizeX <= 0f ? 40f : b.sizeX;
            m.sizeZ = b.sizeZ <= 0f ? 40f : b.sizeZ;
        }

        private static MapBoundsMarker FindBoundsMarker()
        {
            var root = GameObject.Find(RootName);
            if (root == null) return null;
            var t = root.transform.Find(BoundsName);
            return t != null ? t.GetComponent<MapBoundsMarker>() : null;
        }

        // 마커 위 라벨(Editor 전용 — 런타임 마커는 UnityEditor 참조 금지).
        [DrawGizmo(GizmoType.NonSelected | GizmoType.Selected | GizmoType.Pickable)]
        private static void DrawMarkerLabel(SpawnPointMarker marker, GizmoType type)
        {
            Handles.Label(marker.transform.position + Vector3.up * 0.6f, $"Spawn {marker.transform.GetSiblingIndex()}");
        }

        [DrawGizmo(GizmoType.NonSelected | GizmoType.Selected | GizmoType.Pickable)]
        private static void DrawMonsterLabel(MonsterSpawnMarker marker, GizmoType type)
        {
            Handles.Label(marker.transform.position + Vector3.up * 0.7f, $"M: {marker.monsterId}");
        }

        [DrawGizmo(GizmoType.NonSelected | GizmoType.Selected | GizmoType.Pickable)]
        private static void DrawPatrolLabel(PatrolPointMarker marker, GizmoType type)
        {
            Handles.Label(marker.transform.position + Vector3.up * 0.35f, $"P{marker.transform.GetSiblingIndex()}");
        }
    }
}
