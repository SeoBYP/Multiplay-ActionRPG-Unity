using UnityEngine;
using UnityEditor;

namespace MagicPigGames.Shared
{
    public class MissingComponentRemover
    {
        [MenuItem("Window/Magic Pig Games/Remove Missing Components")]
        private static void RemoveMissingComponents()
        {
            GameObject[] selectedObjects = Selection.gameObjects;

            foreach (GameObject obj in selectedObjects)
            {
                RemoveMissingComponentsInGameObject(obj);
                foreach (Transform child in obj.transform)
                {
                    RemoveMissingComponentsInGameObject(child.gameObject);
                }
            }

            Debug.Log("Missing components removed from selected objects and their children.");
        }

        [MenuItem("Window/Magic Pig Games/Remove Missing Components", true)]
        private static bool ValidateRemoveMissingComponents()
        {
            return Selection.gameObjects.Length > 0;
        }

        private static void RemoveMissingComponentsInGameObject(GameObject obj)
        {
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(obj);
        }
    }
}