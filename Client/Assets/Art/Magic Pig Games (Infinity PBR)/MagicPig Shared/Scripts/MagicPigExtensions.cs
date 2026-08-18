using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using Random = UnityEngine.Random;

namespace MagicPigGames.Shared
{
    public static class MagicPigExtensions
    {
        private static readonly System.Random _rng = new();

        /// <summary>
        ///     Returns the number of layers selected in the LayerMask
        /// </summary>
        /// <param name="layerMask"></param>
        /// <returns></returns>
        public static int CountLayers(this LayerMask layerMask)
        {
            var count = 0;
            for (var i = 0; i < 32; i++)
            {
                var shifted = 1 << i;
                if ((layerMask.value & shifted) == shifted)
                    count++;
            }

            return count;
        }

        /// <summary>
        ///     Returns true if the compareValue is the same as the value
        /// </summary>
        /// <param name="value"></param>
        /// <param name="compareValue"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static bool Is<T>(this T value, T compareValue)
        {
            return EqualityComparer<T>.Default.Equals(value, compareValue);
        }

        /// <summary>
        ///     Returns true if the compareValue is NOT the same as the value
        /// </summary>
        /// <param name="value"></param>
        /// <param name="compareValue"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static bool IsNot<T>(this T value, T compareValue)
        {
            return !EqualityComparer<T>.Default.Equals(value, compareValue);
        }

        // I believe these are redundant now with the above versions.
        /*
        /// <summary>
        /// Returns true if the strings are the same
        /// </summary>
        /// <param name="value"></param>
        /// <param name="compareValue"></param>
        /// <returns></returns>
        public static bool Is(this string value, string compareValue) => value == compareValue;

        /// <summary>
        /// Returns true if the strings are not the same
        /// </summary>
        /// <param name="value"></param>
        /// <param name="compareValue"></param>
        /// <returns></returns>
        public static bool IsNot(this string value, string compareValue) => value != compareValue;

        /// <summary>
        /// Returns true if the bools are the same
        /// </summary>
        /// <param name="value"></param>
        /// <param name="compareValue"></param>
        /// <returns></returns>
        public static bool Is(this int value, int compareValue) => value == compareValue;

        /// <summary>
        /// Returns true if the bools are not the same
        /// </summary>
        /// <param name="value"></param>
        /// <param name="compareValue"></param>
        /// <returns></returns>
        public static bool IsNot(this int value, int compareValue) => value != compareValue;
        */

        public static IEnumerable<T> TakeRandomXAmount<T>(this IList<T> items, int amount)
        {
            if (amount >= items.Count) amount = items.Count;
            for (var i = 0; i < amount; i++)
                yield return items.TakeRandom();
        }

        /// <summary>
        ///     Returns a random item from the list
        /// </summary>
        /// <param name="list"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static T TakeRandom<T>(this IList<T> list)
        {
            return list[Random.Range(0, list.Count)];
        }

        /// <summary>
        ///     Returns a random index from the list
        /// </summary>
        /// <param name="list"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static int TakeRandomIndex<T>(this IList<T> list)
        {
            return Random.Range(0, list.Count);
        }

        /// <summary>
        ///     Returns a random item and index from the list
        /// </summary>
        /// <param name="list"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static (T, int) TakeRandomAndIndex<T>(this IList<T> list)
        {
            var random = Random.Range(0, list.Count);
            return (list[random], random);
        }

        /// <summary>
        ///     Randomizes the list. Optionally provide a start index. Items before the start index will not be randomized.
        ///     If fromEnd is true, then the last X items will be retained, while items before will be shuffled.
        /// </summary>
        /// <param name="list"></param>
        /// <param name="startOrEndIndex"></param>
        /// <param name="fromEnd"></param>
        /// <typeparam name="T"></typeparam>
        public static void Shuffle<T>(this List<T> list, int startOrEndIndex = 0, bool fromEnd = false)
        {
            var startIndex = fromEnd ? list.Count - startOrEndIndex : startOrEndIndex;

            var n = list.Count;
            while (n > startIndex + 1)
            {
                n--;
                var k = _rng.Next(startIndex, n + 1);
                (list[k], list[n]) = (list[n], list[k]);
            }
        }

        /// <summary>
        ///     Reverse the order of the list. Optionally provide a start index. Items before the start index will not be reversed.
        /// </summary>
        /// <param name="list"></param>
        /// <param name="startIndex"></param>
        /// <typeparam name="T"></typeparam>
        public static void Reverse<T>(this List<T> list, int startIndex = 0)
        {
            var i = startIndex;
            var j = list.Count - 1;
            while (i < j)
            {
                (list[i], list[j]) = (list[j], list[i]);
                i++;
                j--;
            }
        }

        /// <summary>
        ///     Adds an item to the list if it doesn't already exist
        /// </summary>
        /// <param name="list"></param>
        /// <param name="item"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static bool AddDistinct<T>(this List<T> list, T item)
        {
            if (list.Contains(item))
                return false;

            list.Add(item);
            return true;
        }

        /// <summary>
        ///     Creates a shallow clone. Reference types will still point to the same objects.
        /// </summary>
        /// <param name="list"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static List<T> Clone<T>(this IEnumerable<T> list)
        {
            return list.ToList();
        }

        /// <summary>
        ///     Rounds a float to a specified number of decimal places
        /// </summary>
        /// <param name="value"></param>
        /// <param name="decimals"></param>
        /// <returns></returns>
        // MathF.Pow(10, decimalPlaces) computes 10^decimalPlaces, which is used to shift the decimal point
        // MathF.Round rounds to the nearest integer
        // Then divide again by 10^decimalPlaces to shift the decimal point back
        public static float RoundToDecimal(this float value, int decimals = 2)
        {
            return Mathf.Round(value * Mathf.Pow(10, decimals)) / Mathf.Pow(10, decimals);
        }

        public static string StringWithColor<T>(this T value, string color) where T : IFormattable
        {
            return $"<color={color}>{value}</color>";
        }

        public static int WholeDigits(this float value)
        {
            var str = value.ToString(CultureInfo.InvariantCulture);
            return str.IndexOf('.') != -1 ? str.IndexOf('.') : str.Length;
        }

        public static int TotalDigits(this float value, int limitDecimalCount = -1)
        {
            var str = value.ToString(CultureInfo.InvariantCulture);

            //Determine the position of the decimal point if it's present
            var decimalPointPosition = str.IndexOf('.');

            if (decimalPointPosition == -1)
                return str.Length;

            // There is a decimal point, so subtract one to not count the decimal itself
            var count = str.Length - 1;
            if (limitDecimalCount < 0) return count;

            // limit the decimal count if necessary
            var decimalCount = str.Length - decimalPointPosition - 1;
            count -= Math.Max(decimalCount - limitDecimalCount, 0);
            return count;
        }

        public static string Truncate(this string value, int length = 25, bool addEllipsis = true)
        {
            if (value.Length <= length)
                return value;

            return addEllipsis ? value[..length] + "..." : value[..length];
        }

        public static string TrimSpaces(this string str)
        {
            return str.Trim();
        }

        public static string NoSpaces(this string str)
        {
            return str.Replace(" ", "");
        }

        public static string SafeString(this string str)
        {
            return Regex.Replace(str, "[^a-zA-Z0-9_-]", "");
        }

        public static string SafeStringWithoutSpaces(this string str)
        {
            return str.NoSpaces().SafeString();
        }

        public static string SpacesBeforeCapitals(this string str)
        {
            var newText = new StringBuilder(str.Length * 2);
            newText.Append(str[0]);

            for (var i = 1; i < str.Length; i++)
            {
                if (char.IsUpper(str[i]) && !char.IsWhiteSpace(str[i - 1]))
                    newText.Append(' ');
                newText.Append(str[i]);
            }

            return newText.ToString();
        }

        public static bool Between(this float value, float a, float b)
        {
            return value >= Mathf.Min(a, b) && value <= Mathf.Max(a, b);
        }

        public static bool BetweenAngle(this float value, float a, float b)
        {
            value = (value + 360) % 360;
            a = (a + 360) % 360;
            b = (b + 360) % 360;

            if (a > b)
                // If "a" is bigger, it means we've wrapped around the circle.
                // So, we need to verify if "value" is not within (b, a)
                return !(value > b && value < a);
            // Otherwise, ensure "value" is between (a, b).
            return value >= a && value <= b;
        }

        public static Color Saturate(this Color value, float saturation, float factorR = 0.299f, float factorG = 0.587f,
            float factorB = 0.114f)
        {
            // Clamp the saturation value to be between 0 and 1 to ensure that no invalid values are used.
            saturation = Mathf.Clamp01(saturation);

            // Calculate the grayscale equivalent using the specified factors.
            var greyScale = factorR * value.r + factorG * value.g + factorB * value.b;

            // Interpolate between the grayscale and the original color using the saturation level,
            // then clamp the results to ensure they are within the 0-1 range.
            return new Color(
                Mathf.Clamp01(Mathf.Lerp(greyScale, value.r, saturation)),
                Mathf.Clamp01(Mathf.Lerp(greyScale, value.g, saturation)),
                Mathf.Clamp01(Mathf.Lerp(greyScale, value.b, saturation)),
                Mathf.Clamp01(value
                    .a) // Normally the alpha value should already be between 0 and 1, but we clamp it as well just in case.
            );
        }

        public static bool IsSimilarTo(this Color value, Color compareColor, float tolerance = 0.01f)
        {
            return Mathf.Abs(value.r - compareColor.r) < tolerance
                   && Mathf.Abs(value.g - compareColor.g) < tolerance
                   && Mathf.Abs(value.b - compareColor.b) < tolerance
                   && Mathf.Abs(value.a - compareColor.a) < tolerance;
        }
    }
}