using UnityEditor;

namespace MagicPigGames.Shared
{
    /// <summary>
    /// Keeps the shared label cache (MagicPigStatic) current without forcing a full project rescan on every
    /// inspector enable. Whenever assets are imported / deleted / moved (which is also what fires when an asset's
    /// labels change), the cache is invalidated so the next GetAllLabels()/FindAssetsByLabel() rebuilds it lazily.
    /// </summary>
    public class MagicPigLabelCacheInvalidator : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            // Cheap: just nulls the static caches; the (expensive) rebuild happens lazily only when next needed.
            MagicPigStatic.InvalidateLabelCache();
        }
    }
}
