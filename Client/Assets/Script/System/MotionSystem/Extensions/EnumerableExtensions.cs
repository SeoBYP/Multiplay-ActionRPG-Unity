using System;
using System.Collections.Generic;

namespace Game.System.MotionSystem
{
    /// <summary>
    /// Motion Matching 코드에서 반복적으로 필요한 IEnumerable 유틸리티입니다.
    /// Unity/.NET 버전에 따라 기본 제공되지 않는 LINQ 기능을 프로젝트 내부에서 보완합니다.
    /// </summary>
    public static class EnumerableExtensions
    {
        /// <summary>
        /// selector 값이 가장 작은 원소를 반환합니다.
        /// pose 후보 중 distance score가 가장 낮은 frame을 찾을 때 사용할 수 있습니다.
        /// </summary>
        public static TSource MinBy<TSource, TKey>(
            this IEnumerable<TSource> source,
            Func<TSource, TKey> selector)
        {
            return source.MinBy(selector, null);
        }

        /// <summary>
        /// comparer 기준으로 selector 값이 가장 작은 원소를 반환합니다.
        /// 빈 sequence는 InvalidOperationException을 던집니다.
        /// </summary>
        public static TSource MinBy<TSource, TKey>(
            this IEnumerable<TSource> source,
            Func<TSource, TKey> selector,
            IComparer<TKey> comparer)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (selector == null) throw new ArgumentNullException(nameof(selector));
            comparer ??= Comparer<TKey>.Default;

            using (var sourceIterator = source.GetEnumerator())
            {
                if (!sourceIterator.MoveNext())
                {
                    throw new InvalidOperationException("Sequence contains no elements");
                }

                var min = sourceIterator.Current;
                var minKey = selector(min);
                while (sourceIterator.MoveNext())
                {
                    var candidate = sourceIterator.Current;
                    var candidateProjected = selector(candidate);
                    if (comparer.Compare(candidateProjected, minKey) < 0)
                    {
                        min = candidate;
                        minKey = candidateProjected;
                    }
                }

                return min;
            }
        }

        /// <summary>
        /// IEnumerable의 각 원소에 action을 실행합니다.
        /// 디버그 문자열 조립처럼 짧은 순회 코드를 간결하게 만들 때 사용합니다.
        /// </summary>
        public static void ForEach<T>(this IEnumerable<T> enumeration, Action<T> action)
        {
            foreach (var item in enumeration)
            {
                action(item);
            }
        }

        /// <summary>
        /// enum 값을 다음 enum 값으로 순환시킵니다.
        /// 마지막 값 이후에는 첫 값으로 돌아갑니다.
        /// </summary>
        public static T Next<T>(this T src) where T : struct
        {
            if (!typeof(T).IsEnum) throw new ArgumentException($"Argument {typeof(T).FullName} is not an Enum");

            T[] arr = (T[])Enum.GetValues(src.GetType());
            int j = Array.IndexOf(arr, src) + 1;
            return arr.Length == j ? arr[0] : arr[j];
        }
    }
}
