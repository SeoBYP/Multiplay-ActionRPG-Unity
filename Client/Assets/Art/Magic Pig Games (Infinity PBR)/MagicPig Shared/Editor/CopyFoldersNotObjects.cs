using UnityEngine;
using UnityEditor;
using System.IO;

namespace MagicPigGames.Shared
{
    public class CopyFoldersNotObjects
    {
        [MenuItem("Window/Magic Pig Games/Copy Folders (Not Objects)")]
        private static void CopyFoldersWithoutObjects()
        {
            // Get selected folders
            var selectedFolders = Selection.GetFiltered<UnityEngine.Object>(SelectionMode.Assets);

            foreach (var selectedFolder in selectedFolders)
            {
                string sourcePath = AssetDatabase.GetAssetPath(selectedFolder);
                if (Directory.Exists(sourcePath))
                {
                    string destinationPath = sourcePath + " copy";
                    CopyFolderStructure(sourcePath, destinationPath);
                }
            }
        }

        private static void CopyFolderStructure(string sourcePath, string destinationPath)
        {
            // Create the destination folder
            Directory.CreateDirectory(destinationPath);

            // Get all subdirectories in the source path
            var directories = Directory.GetDirectories(sourcePath, "*", SearchOption.AllDirectories);

            // Create the subdirectory structure in the destination path
            foreach (var directory in directories)
            {
                string relativePath = directory.Substring(sourcePath.Length + 1);
                string newDirectoryPath = Path.Combine(destinationPath, relativePath);
                Directory.CreateDirectory(newDirectoryPath);
            }

            // Refresh the AssetDatabase to show the new folders in the Project window
            AssetDatabase.Refresh();
        }

        [MenuItem("Window/Magic Pig Games/Copy Folders (Not Objects)", true)]
        private static bool ValidateFoldersSelected()
        {
            // Get selected objects
            var selectedObjects = Selection.GetFiltered<UnityEngine.Object>(SelectionMode.Assets);

            // Return true if all selected objects are folders
            foreach (var selectedObject in selectedObjects)
            {
                string path = AssetDatabase.GetAssetPath(selectedObject);
                if (!Directory.Exists(path))
                {
                    return false;
                }
            }

            return selectedObjects.Length > 0;
        }
    }
}
