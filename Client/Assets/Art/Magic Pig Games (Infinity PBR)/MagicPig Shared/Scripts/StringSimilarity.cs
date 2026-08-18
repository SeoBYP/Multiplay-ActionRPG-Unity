using System.Collections;
using System.Collections.Generic;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace MagicPigGames.Shared
{
    public static class StringSimilarity
    {
        public enum SimilarityAlgorithm
        {
            Levenshtein,
            JaroWinkler,
            Cosine,
            Jaccard
        }
        
        // ------------------------------
        // EDITOR PREFS - Save these values, and use them in the Editor scripts. The methods here will not use them,
        // but your own scripts can call upon these saved values. At runtime, the PlayerPrefs will be used. If you do
        // not want the default values below to be used, be sure to set them in your application.
        // ------------------------------
        
        public static float BestMatchThreshold
        {
            get
            {
#if UNITY_EDITOR
                return UnityEditor.EditorPrefs.GetFloat("BestMatchThreshold", 75.0f);
#else
                return UnityEngine.PlayerPrefs.GetFloat("BestMatchThreshold", 75.0f);
#endif
            }
            set
            {
#if UNITY_EDITOR
                UnityEditor.EditorPrefs.SetFloat("BestMatchThreshold", value);
#else
                UnityEngine.PlayerPrefs.SetFloat("BestMatchThreshold", value);
#endif
            }
        }

        public static SimilarityAlgorithm BestMatchAlgorithm
        {
            get
            {
#if UNITY_EDITOR
                return (SimilarityAlgorithm)UnityEditor.EditorPrefs.GetInt("BestMatchAlgorithm", (int)SimilarityAlgorithm.JaroWinkler);
#else
                return (SimilarityAlgorithm)UnityEngine.PlayerPrefs.GetInt("BestMatchAlgorithm", (int)SimilarityAlgorithm.JaroWinkler);
#endif
            }
            set
            {
#if UNITY_EDITOR
                UnityEditor.EditorPrefs.SetInt("BestMatchAlgorithm", (int)value);
#else
                UnityEngine.PlayerPrefs.SetInt("BestMatchAlgorithm", (int)value);
#endif
            }
        }
        
        // ------------------------------
        // PUBLIC METHODS
        // ------------------------------

        public static string FindBestMatch(string target, List<string> candidates, SimilarityAlgorithm algorithm = SimilarityAlgorithm.Levenshtein)
        {
            string bestMatch = null;
            var highestSimilarity = 0.0f;

            foreach (var candidate in candidates)
            {
                var similarity = SimilarityPercentage(target, candidate, algorithm);
                if (!(similarity > highestSimilarity))
                    continue;

                highestSimilarity = similarity;
                bestMatch = candidate;
            }

            return bestMatch;
        }

        public static string FindBestMatch(this List<string> candidates, string target, SimilarityAlgorithm algorithm = SimilarityAlgorithm.Levenshtein)
            => FindBestMatch(target, candidates, algorithm);

        public static List<string> FindBestMatches(string target, List<string> candidates, float threshold = 75, SimilarityAlgorithm algorithm = SimilarityAlgorithm.Levenshtein)
        {
            var matches = new List<KeyValuePair<string, float>>();

            foreach (var candidate in candidates)
            {
                var similarity = SimilarityPercentage(target, candidate, algorithm);
                if (!(similarity >= threshold))
                    continue;

                matches.Add(new KeyValuePair<string, float>(candidate, similarity));
            }

            matches.Sort((pair1, pair2) => pair2.Value.CompareTo(pair1.Value));

            return matches.Select(pair => pair.Key).ToList();
        }

        public static List<string> FindBestMatches(this List<string> candidates, string target, float threshold = 75, SimilarityAlgorithm algorithm = SimilarityAlgorithm.Levenshtein)
            => FindBestMatches(target, candidates, threshold, algorithm);

        // ------------------------------
        // VARIOUS SIMILARITY ALGORITHMS
        // ------------------------------
        
        public static int LevenshteinDistance(string a, string b)
        {
            if (string.IsNullOrEmpty(a))
                return !string.IsNullOrEmpty(b) ? b.Length : 0;

            if (string.IsNullOrEmpty(b))
                return !string.IsNullOrEmpty(a) ? a.Length : 0;

            var lengthA = a.Length;
            var lengthB = b.Length;

            var matrix = new int[lengthA + 1, lengthB + 1];

            for (var i = 0; i <= lengthA; matrix[i, 0] = i++) { }
            for (var j = 0; j <= lengthB; matrix[0, j] = j++) { }

            for (var i = 1; i <= lengthA; i++)
            {
                for (var j = 1; j <= lengthB; j++)
                {
                    var cost = (b[j - 1] == a[i - 1]) ? 0 : 1;
                    matrix[i, j] = Mathf.Min(
                        Mathf.Min(matrix[i - 1, j] + 1, matrix[i, j - 1] + 1),
                        matrix[i - 1, j - 1] + cost);
                }
            }

            return matrix[lengthA, lengthB];
        }

        public static float JaroWinklerDistance(string s1, string s2)
        {
            if (s1 == s2)
                return 1.0f;

            int s1_len = s1.Length;
            int s2_len = s2.Length;

            if (s1_len == 0 || s2_len == 0)
                return 0.0f;

            int match_distance = Mathf.Max(s1_len, s2_len) / 2 - 1;

            bool[] s1_matches = new bool[s1_len];
            bool[] s2_matches = new bool[s2_len];

            int matches = 0;
            int transpositions = 0;

            for (int i = 0; i < s1_len; i++)
            {
                int start = Mathf.Max(0, i - match_distance);
                int end = Mathf.Min(i + match_distance + 1, s2_len);

                for (int j = start; j < end; j++)
                {
                    if (s2_matches[j]) continue;
                    if (s1[i] != s2[j]) continue;
                    s1_matches[i] = true;
                    s2_matches[j] = true;
                    matches++;
                    break;
                }
            }

            if (matches == 0) return 0.0f;

            int k = 0;
            for (int i = 0; i < s1_len; i++)
            {
                if (!s1_matches[i]) continue;
                while (!s2_matches[k]) k++;
                if (s1[i] != s2[k]) transpositions++;
                k++;
            }

            float m = matches;
            float jaro = ((m / s1_len) + (m / s2_len) + ((m - transpositions / 2.0f) / m)) / 3.0f;

            int prefix = 0;
            for (int i = 0; i < Mathf.Min(4, s1_len); i++)
            {
                if (s1[i] == s2[i])
                    prefix++;
                else
                    break;
            }

            return jaro + 0.1f * prefix * (1 - jaro);
        }
        
        public static float CosineSimilarity(string a, string b)
        {
            var vec1 = GetTermFrequencyVector(a);
            var vec2 = GetTermFrequencyVector(b);

            var dotProduct = vec1.Zip(vec2, (v1, v2) => v1 * v2).Sum();
            var magnitudeA = Mathf.Sqrt(vec1.Sum(v => v * v));
            var magnitudeB = Mathf.Sqrt(vec2.Sum(v => v * v));

            return dotProduct / (magnitudeA * magnitudeB);
        }

        private static int[] GetTermFrequencyVector(string s)
        {
            var terms = s.ToLower().GroupBy(c => c).ToDictionary(g => g.Key, g => g.Count());
            var vector = new int[26]; // assuming only lowercase letters

            for (char c = 'a'; c <= 'z'; c++)
            {
                vector[c - 'a'] = terms.ContainsKey(c) ? terms[c] : 0;
            }

            return vector;
        }
        
        public static float JaccardIndex(string a, string b)
        {
            var setA = new HashSet<char>(a);
            var setB = new HashSet<char>(b);

            var intersection = new HashSet<char>(setA);
            intersection.IntersectWith(setB);

            var union = new HashSet<char>(setA);
            union.UnionWith(setB);

            return (float)intersection.Count / union.Count;
        }

        public static float SimilarityPercentage(string a, string b, SimilarityAlgorithm algorithm = SimilarityAlgorithm.Levenshtein)
        {
            switch (algorithm)
            {
                case SimilarityAlgorithm.JaroWinkler:
                    return JaroWinklerDistance(a, b) * 100.0f;
                case SimilarityAlgorithm.Cosine:
                    return CosineSimilarity(a, b) * 100.0f;
                case SimilarityAlgorithm.Jaccard:
                    return JaccardIndex(a, b) * 100.0f;
                case SimilarityAlgorithm.Levenshtein:
                default:
                    var maxLen = Mathf.Max(a.Length, b.Length);
                    if (maxLen == 0) return 100.0f;
                    var distance = LevenshteinDistance(a, b);
                    return (1.0f - (float)distance / maxLen) * 100.0f;
            }
        }
    }
}
