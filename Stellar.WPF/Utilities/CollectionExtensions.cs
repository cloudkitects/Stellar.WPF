using System.Collections.Generic;

namespace Stellar.WPF.Utilities
{
    internal static class CollectionExtensions
    {
        #region IList
        public static void AddIfNotExists<T>(this IList<T> list, T obj)
        {
            if (!list.Contains(obj))
            {
                list.Add(obj);
            }
        }
        #endregion
    }
}
