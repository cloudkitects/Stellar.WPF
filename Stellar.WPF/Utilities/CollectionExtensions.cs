using System.Collections.Generic;

namespace Stellar.WPF.Utilities
{
    internal static class CollectionExtensions
    {
        public static void AddIfNotExists<T>(this IList<T> list, T obj)
        {
            if (!list.Contains(obj))
            {
                list.Add(obj);
            }
        }

        public static void AddRange<T>(this ICollection<T> collection, IEnumerable<T> elements)
        {
            foreach (T e in elements)
            {
                collection.Add(e);
            }
        }

        /// <summary>
        /// Creates an IEnumerable with a single value.
        /// </summary>
        public static IEnumerable<T> ToEnumerable<T>(this T value)
        {
            yield return value;
        }
    }
}
