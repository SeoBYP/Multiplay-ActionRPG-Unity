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
        private const string RootName   = "[MapAuthoring]";
        private const string SpawnsName = "Spawns";

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
                if (GUILayout.Button("2) Save to SO & Export JSON")) SaveAndExport();
            }

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "1) MapDefinition 선택 → Load to Scene\n" +
                "2) 마커(파란 구) 드래그로 배치, 필요시 Add Spawn Point\n" +
                "3) Save to SO & Export → SO + 클라/서버 JSON 갱신 (서버는 재빌드 시 반영)\n" +
                "스폰 순서 = 'Spawns' 아래 마커 순서(SpawnIndex).",
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

            Selection.activeGameObject = root;
            Debug.Log($"[MapEditorWindow] '{_target.mapId}' 로드 — 스폰 {pts.Count}개");
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

            EditorUtility.SetDirty(_target);
            AssetDatabase.SaveAssets();

            var count = MapDataExporter.BakeAll();
            Debug.Log($"[MapEditorWindow] '{_target.mapId}' 저장 — 스폰 {markers.Count}개 → SO + JSON(맵 {count}개) export 완료.");
            EditorUtility.DisplayDialog("Save & Export",
                $"'{_target.mapId}' 스폰 {markers.Count}개를 SO에 저장하고 JSON으로 export 했습니다.\n서버 반영은 서버 재빌드 필요.", "확인");
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

        private static Transform FindSpawns(bool createIfMissing)
        {
            var root = GameObject.Find(RootName);
            if (root == null)
            {
                if (!createIfMissing) return null;
                root = new GameObject(RootName);
            }
            var spawns = root.transform.Find(SpawnsName);
            if (spawns == null && createIfMissing)
            {
                var go = new GameObject(SpawnsName);
                go.transform.SetParent(root.transform, false);
                spawns = go.transform;
            }
            return spawns;
        }

        // 마커 위에 SpawnIndex 라벨(Editor 전용 — 런타임 마커는 UnityEditor 참조 금지).
        [DrawGizmo(GizmoType.NonSelected | GizmoType.Selected | GizmoType.Pickable)]
        private static void DrawMarkerLabel(SpawnPointMarker marker, GizmoType type)
        {
            Handles.Label(marker.transform.position + Vector3.up * 0.6f, $"Spawn {marker.transform.GetSiblingIndex()}");
        }
    }
}
