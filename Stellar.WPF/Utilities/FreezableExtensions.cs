using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Stellar.WPF.Utilities
{
    internal static class FreezableExtensions
    {
        public static void ThrowIfFrozen(this IFreezable freezable)
        {
            if (freezable.IsFrozen)
            {
                throw new InvalidOperationException($"Cannot mutate frozen {freezable.GetType().Name}.");
            }
        }

        public static IList<T> DeepFreeze<T>(this IList<T>? list)
        {
            if (list != null)
            {
                foreach (T item in list)
                {
                    Freeze(item);
                }
            }
            return Freeze(list);
        }

        /// <summary>
        /// Freeze a  list.
        /// </summary>
        /// <remarks>
        /// It returns read-only lists directly to avoid undoing the effects of interning.
        /// </remarks>
        public static IList<T> Freeze<T>(this IList<T>? list)
        {
            if (list is null || list.Count == 0)
            {
                return Array.Empty<T>();
            }

            if (list.IsReadOnly)
            {
                return list;
            }
            else
            {
                return new ReadOnlyCollection<T>(list);
            }
        }

        public static void Freeze(this object? obj)
        {
            if (obj is IFreezable freezable)
            {
                freezable.Freeze();
            }
        }

        public static T Freeze<T>(this T item) where T : IFreezable
        {
            item.Freeze();

            return item;
        }

        /// <summary>
        /// Return a cloned and frozen item--itself if already frozen.
        /// </summary>
        public static T Clone<T>(this T item) where T : IFreezable, ICloneable
        {
            if (!item.IsFrozen)
            {
                item = (T)item.Clone();
                item.Freeze();
            }

            return item;
        }
    }
}
