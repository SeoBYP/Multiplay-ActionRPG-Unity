using System.Collections.Generic;
using UnityEngine;

namespace MagicPigGames.Shared
{
    public static class EnumerableExtensions
    {
        /*
         * NOTE: This is now moved to the MagicPigExtensions.cs class. I will remove this in a future update, but am
         * leaving it here so that people get the update with the commented out code, rather than getting an error.
         */
        
        /*
        public static T TakeRandom<T>(this IList<T> items) => items[Random.Range(0, items.Count)];

        public static IEnumerable<T> TakeRandomXAmount<T>(this IList<T> items, int amount)
        {
            if (amount >= items.Count) amount = items.Count;
            for (int i = 0; i < amount; i++)
                yield return items.TakeRandom();
        }
        */

    }
}