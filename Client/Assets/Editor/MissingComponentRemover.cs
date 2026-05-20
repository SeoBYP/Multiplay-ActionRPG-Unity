using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MissingComponentRemover
{
    private const string MenuPath = "GameObject/Remove Missing Scripts";

    [MenuItem(MenuPath, false, 0)]
    private static void RemoveMissingScripts()
    {
        GameObject[] selectedObjects = Selection.gameObjects;
        if (selectedObjects.Length == 0)
        {
            Debug.LogWarning("[MissingComponentRemover] No GameObjects selected.");
            return;
        }

        int totalRemoved = 0;

        foreach (GameObject go in selectedObjects)
        {
            Undo.RegisterFullObjectHierarchyUndo(go, "Remove Missing Scripts");
            totalRemoved += RemoveRecursive(go);
        }

        PrefabStage prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
        if (prefabStage != null)
            EditorSceneManager.MarkSceneDirty(prefabStage.scene);

        Debug.Log(totalRemoved > 0
            ? $"[MissingComponentRemover] {totalRemoved}개의 Missing Script를 제거했습니다."
            : "[MissingComponentRemover] Missing Script가 없습니다.");
    }

    [MenuItem(MenuPath, true)]
    private static bool ValidateRemoveMissingScripts() => Selection.gameObjects.Length > 0;

    private static int RemoveRecursive(GameObject go)
    {
        int count = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
        foreach (Transform child in go.transform)
            count += RemoveRecursive(child.gameObject);
        return count;
    }
}
