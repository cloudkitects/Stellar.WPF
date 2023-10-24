using System;
using System.Collections.ObjectModel;

namespace Stellar.WPF.Utilities;

/// <summary>
/// A collection that cannot contain null values.
/// </summary>
[Serializable]
public class NullSafeCollection<T> : Collection<T> where T : class
{
    /// <inheritdoc/>
    protected override void InsertItem(int index, T item)
    {
        base.InsertItem(index, item ?? throw new ArgumentNullException(nameof(item)));
    }

    /// <inheritdoc/>
    protected override void SetItem(int index, T item)
    {
        base.SetItem(index, item ?? throw new ArgumentNullException(nameof(item)));
    }
}