using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// TextMeshProUGUI의 FontAsset을 일괄 교체하는 에디터 툴.
/// Tools > TMP Font Replacer
/// </summary>
public class TMP_FontReplacer : EditorWindow
{
    private enum SearchScope { Selection, CurrentScene, PrefabsInFolder }

    private TMP_FontAsset _fromFont;   // null = 모든 폰트 교체
    private TMP_FontAsset _toFont;
    private SearchScope   _scope = SearchScope.Selection;
    private string        _folderPath = "Assets/Prefabs";

    private readonly List<(Object target, TMP_FontAsset old)> _preview = new();
    private Vector2 _scroll;
    private bool    _previewDirty = true;

    [MenuItem("Tools/TMP Font Replacer")]
    private static void Open() => GetWindow<TMP_FontReplacer>("TMP Font Replacer");

    private void OnGUI()
    {
        EditorGUILayout.Space(4);

        // ── 교체 설정 ──────────────────────────────────────
        EditorGUI.BeginChangeCheck();

        _toFont = (TMP_FontAsset)EditorGUILayout.ObjectField(
            "교체할 폰트 (New)", _toFont, typeof(TMP_FontAsset), false);

        EditorGUILayout.Space(2);
        EditorGUILayout.LabelField("필터 (비워두면 모든 폰트 교체)", EditorStyles.miniLabel);
        _fromFont = (TMP_FontAsset)EditorGUILayout.ObjectField(
            "기존 폰트 (From)", _fromFont, typeof(TMP_FontAsset), false);

        EditorGUILayout.Space(6);
        _scope = (SearchScope)EditorGUILayout.EnumPopup("검색 범위", _scope);

        if (_scope == SearchScope.PrefabsInFolder)
        {
            EditorGUILayout.BeginHorizontal();
            _folderPath = EditorGUILayout.TextField("폴더", _folderPath);
            if (GUILayout.Button("선택", GUILayout.Width(48)))
            {
                var path = EditorUtility.OpenFolderPanel("폴더 선택", _folderPath, "");
                if (!string.IsNullOrEmpty(path))
                    _folderPath = "Assets" + path.Substring(Application.dataPath.Length);
            }
            EditorGUILayout.EndHorizontal();
        }

        if (EditorGUI.EndChangeCheck()) _previewDirty = true;

        EditorGUILayout.Space(8);

        // ── 버튼 ───────────────────────────────────────────
        using (new EditorGUI.DisabledScope(_toFont == null))
        {
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("미리보기", GUILayout.Height(28)))
            {
                BuildPreview();
                _previewDirty = false;
            }

            GUI.backgroundColor = _preview.Count > 0 ? new Color(0.6f, 1f, 0.6f) : Color.white;
            if (GUILayout.Button($"교체 ({(_previewDirty ? "?" : _preview.Count.ToString())}개)", GUILayout.Height(28)))
            {
                if (_previewDirty) BuildPreview();
                Apply();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndHorizontal();
        }

        if (_toFont == null)
            EditorGUILayout.HelpBox("교체할 폰트를 지정하세요.", MessageType.Info);

        // ── 미리보기 목록 ──────────────────────────────────
        if (_preview.Count > 0)
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField($"대상 {_preview.Count}개", EditorStyles.boldLabel);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (var (target, old) in _preview)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.ObjectField(target, typeof(TMP_FontAsset), true, GUILayout.Width(200));
                EditorGUILayout.LabelField($"{old?.name ?? "(없음)"} → {_toFont.name}", EditorStyles.miniLabel);
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();
        }
    }

    // ── 미리보기 수집 ───────────────────────────────────────

    private void BuildPreview()
    {
        _preview.Clear();
        foreach (var tmp in Collect())
        {
            if (_fromFont != null && tmp.font != _fromFont) continue;
            _preview.Add((tmp, tmp.font));
        }
    }

    // ── 교체 실행 ──────────────────────────────────────────

    private void Apply()
    {
        if (_preview.Count == 0) { Debug.Log("[TMP FontReplacer] 교체 대상 없음"); return; }

        int count = 0;
        foreach (var tmp in Collect())
        {
            if (_fromFont != null && tmp.font != _fromFont) continue;

            Undo.RecordObject(tmp, "Replace TMP Font");
            tmp.font = _toFont;
            EditorUtility.SetDirty(tmp);
            count++;
        }

        // 씬/프리팹 스테이지 저장 처리
        var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
        if (prefabStage != null)
            EditorSceneManager.MarkSceneDirty(prefabStage.scene);
        else if (_scope == SearchScope.CurrentScene)
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

        AssetDatabase.SaveAssets();

        Debug.Log($"[TMP FontReplacer] {count}개 교체 완료 → {_toFont.name}");
        _preview.Clear();
        _previewDirty = true;
    }

    // ── 대상 TextMeshProUGUI 수집 ──────────────────────────

    private IEnumerable<TextMeshProUGUI> Collect()
    {
        switch (_scope)
        {
            case SearchScope.Selection:
                foreach (var go in Selection.gameObjects)
                foreach (var tmp in go.GetComponentsInChildren<TextMeshProUGUI>(true))
                    yield return tmp;
                break;

            case SearchScope.CurrentScene:
                var roots = SceneManager.GetActiveScene().GetRootGameObjects();
                foreach (var root in roots)
                foreach (var tmp in root.GetComponentsInChildren<TextMeshProUGUI>(true))
                    yield return tmp;
                break;

            case SearchScope.PrefabsInFolder:
                var guids = AssetDatabase.FindAssets("t:Prefab", new[] { _folderPath });
                foreach (var guid in guids)
                {
                    var path   = AssetDatabase.GUIDToAssetPath(guid);
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab == null) continue;
                    foreach (var tmp in prefab.GetComponentsInChildren<TextMeshProUGUI>(true))
                        yield return tmp;
                }
                break;
        }
    }
}
