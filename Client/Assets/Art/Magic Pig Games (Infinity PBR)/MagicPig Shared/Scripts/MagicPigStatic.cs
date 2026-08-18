using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using Object = UnityEngine.Object;

/*
 * These are static methods used in the editor scripts from Infinity PBR
 */

namespace MagicPigGames.Shared
{
    [Serializable]
    public static class MagicPigStatic
    {
        
        public static Vector3 WorldPositionOf(Transform transform, Vector3 positionOffset) 
            => transform.TransformPoint(positionOffset);
        
        public static bool ContainsTheSameAs<T>(this IEnumerable<T> first, IEnumerable<T> second) 
            => first.ContainsTheSameAs(second, EqualityComparer<T>.Default);

        public static bool ContainsTheSameAs<T>(this IEnumerable<T> first, IEnumerable<T> second, IEqualityComparer<T> comparer)
        {
            if (first == null || second == null) return false;

            var firstAsHashSet = new HashSet<T>(first, comparer);
            var secondAsHashSet = new HashSet<T>(second, comparer);

            return firstAsHashSet.SetEquals(secondAsHashSet);
        }
        
#if UNITY_EDITOR
        
        public static string[] AllPrefabGuids => AssetDatabase.FindAssets("t:Prefab");
        public static string[] AllPrefabPaths => AllPrefabGuids.Select(AssetDatabase.GUIDToAssetPath).ToArray();

        public static Object[] FindAssetsByLabel(string label, bool sortAlpha = true)
        {
            var guids = AssetDatabase.FindAssets($"l:{label}");
            var objects = new Object[guids.Length];
            for (var i = 0; i < guids.Length; i++)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                objects[i] = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
            }
            
            return sortAlpha ? objects.OrderBy(o => o.name).ToArray() : objects;
        }
        
        private static List<string> _cachedLabels;
        private static Dictionary<string, Object> _assetCache = null;
        private static Dictionary<string, string[]> _labelCache = null;

        /// <summary>
        /// Returns the list of all labels in the project. If resetCache is true, it will refresh the cache first.
        /// </summary>
        /// <param name="resetCache"></param>
        /// <param name="folderLimitation"></param>
        /// <returns></returns>
        public static List<string> GetAllLabels(bool resetCache = false, string folderLimitation = "")
        {
            if (_cachedLabels != null && !resetCache) return _cachedLabels;

            CacheLabels(folderLimitation);
            return _cachedLabels;
        }

        /// <summary>
        /// Clears the cached label / asset lookups so the next GetAllLabels() / FindAssetsByLabel() call
        /// rebuilds them. Call this when project assets change (see MagicPigLabelCacheInvalidator) instead
        /// of forcing a full rescan on every inspector enable.
        /// </summary>
        public static void InvalidateLabelCache()
        {
            _cachedLabels = null;
            _assetCache = null;
            _labelCache = null;
        }

        // This will cache all the labels on any object that has the label "Infinity" -- we need to narrow
        // down the list to only those, since we're focusing on Infinity compatible assets.
        private static void CacheLabels(string folderLimitation = "")
        {
            var guids = string.IsNullOrWhiteSpace(folderLimitation) 
                ? AssetDatabase.FindAssets("l:Infinity") 
                : AssetDatabase.FindAssets("l:Infinity", new[] { folderLimitation });

            // Use HashSet to automatically ensure uniqueness of labels
            var allLabels = new HashSet<string>();
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<Object>(path);
                var labels = AssetDatabase.GetLabels(asset);
                foreach (var label in labels)
                    allLabels.Add(label);
            }
            
            _cachedLabels = allLabels.OrderBy(l => l).ToList(); // Alphabetize
            _cachedLabels.Remove("Infinity"); // Remove "Infinity" from the list
            _cachedLabels.Insert(0, "Infinity"); // Add "Infinity" to the beginning
        }

        public static Object[] FindAssetsByLabel(int labelMask, string searchString = "", bool requireAll = true, bool sortAlpha = true)
        {
            // Populate caches if not done already
            if (_assetCache == null || _labelCache == null)
            {
                PopulateCaches();
            }

            // Implement this function as per your needs
            var allLabels = GetAllLabels(); 
            // Convert the mask to a list of selected labels
            var selectedLabels = new HashSet<string>();
            for (int i = 0; i < allLabels.Count; i++)
            {
                if ((labelMask & (1 << i)) != 0)
                {
                    selectedLabels.Add(allLabels[i]);
                }
            }

            // Compiled regex for quick usage
            Regex searchStringRegex = !string.IsNullOrWhiteSpace(searchString) 
                ? new Regex(searchString) 
                : null;

            var objects = new HashSet<Object>();

            // Iterate over each asset in the cache
            foreach (var kvp in _assetCache)
            {
                var guid = kvp.Key;
                var asset = kvp.Value;
                var assetLabels = _labelCache.ContainsKey(guid) ? new HashSet<string>(_labelCache[guid]) : new HashSet<string>();
        
                if (asset == null || (searchStringRegex != null && !searchStringRegex.IsMatch(asset.name)))
                    continue;

                // Check if the asset has all the selected labels
                if (selectedLabels.All(label => assetLabels.Contains(label)))
                {
                    objects.Add(asset);
                }
            }
            // Distinct is already handled by HashSet
            return sortAlpha ? objects.OrderBy(o => o.name).ToArray() : objects.ToArray();
        }

        private static void PopulateCaches()
        {
            _assetCache = new Dictionary<string, Object>();
            _labelCache = new Dictionary<string, string[]>();

            var allGuids = AssetDatabase.FindAssets("");
            foreach(var guid in allGuids)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
                var assetLabels = AssetDatabase.GetLabels(asset);

                _assetCache.Add(guid, asset);
                _labelCache.Add(guid, assetLabels);
            }
        }
        
        
        public static Object[] FindAssetsByLabel(string[] labels, string searchString = "", bool requireAll = true, bool sortAlpha = true)
        {
            List<Object> objects = new List<Object>();  // Use List to append items
            string searchFilter = "l:" + string.Join(" l:", labels);
            Debug.Log($"Search filter: {searchFilter}");
            string[] guids = AssetDatabase.FindAssets(searchFilter);
            Regex searchStringRegex = !string.IsNullOrWhiteSpace(searchString) 
                ? new Regex(searchString) 
                : null;
            foreach(string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                Object asset = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
        
                if(asset == null || (searchStringRegex != null && !searchStringRegex.IsMatch(asset.name)))
                    continue;
        
                if(requireAll)
                {
                    // Get all labels for this asset
                    string[] assetLabels = AssetDatabase.GetLabels(asset);
                    // Check if all labels are contained in the assetLabels
                    if(!labels.All(label => assetLabels.Contains(label)))
                        continue;
                }
        
                objects.Add(asset);
            }
            return sortAlpha 
                ? objects.OrderBy(o => o.name).ToArray() 
                : objects.ToArray();
        }
        

        public static void AddLabel(this Object obj, string label)
        {
            var currentLabels = AssetDatabase.GetLabels(obj);
        
            if (!currentLabels.Contains(label))
            {
                List<string> labelList = new List<string>(currentLabels) { label };
                AssetDatabase.SetLabels(obj, labelList.ToArray());
            }
        }
#endif
    }
}
