using System;
using System.Collections.Generic;

namespace Stellar.WPF.Utilities;

/// <summary>
/// A wrapper for a thread-static list of objects with active calls 
/// to prevent re-entry (and a stack overflow) when they have cyclic (aka circular)
/// references, something that cannot be accomplished safely with a boolean.
/// </summary>
/// <remarks>
/// Inline JavaScript embedded in HTML is a non-cyclic reference, whereas
/// inline JSX (if that was a thing) could create one.
/// </remarks>
internal static class Locker
{
    /// <summary>
    /// An entry in the locker for a list of items, marked successful if
    /// it holds them, and that removes the last one on dispose: GC'd only
    /// after empty.
    /// </summary>
    public readonly struct Entry : IDisposable
    {
        private readonly List<object>? objects;

        internal Entry(List<object>? objects) => this.objects = objects;

        public bool Success => objects is not null;

        public void Dispose() => objects?.RemoveAt(objects.Count - 1);
    }

    /// <summary>
    /// The objects locked in this thread
    /// </summary>
    [ThreadStatic] private static List<object>? objects;

    /// <summary>
    /// Try adding an object to the locker.
    /// </summary>
    /// <param name="obj">The object to add to the locker.</param>
    /// <returns>A failed entry if the object is already in the locker,
    /// a successful entry otherwise.</returns>
    public static Entry TryAdd(object obj)
    {
        Locker.objects ??= new List<object>();

        var objects = Locker.objects;
        
        if (objects.Contains(obj))
        {
            return new Entry(null);
        }

        objects.Add(obj);
        
        return new Entry(objects);
    }
}
