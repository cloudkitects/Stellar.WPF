using System;
using System.Collections.ObjectModel;

namespace Stellar.WPF.Utilities;

// TODO: see how this fares against System ObservableCollection<T> with unit tests.
/// <summary>
/// A collection where adding and removing items causes a callback.
/// It is valid for the onAdd callback to throw an exception - this will prevent the new item from
/// being added to the collection.
/// </summary>
sealed class SimpleObservableCollection<T> : Collection<T>
{
    readonly Action<T> onAdd, onRemove;

    /// <summary>
    /// Creates a new ObserveAddRemoveCollection using the specified callbacks.
    /// </summary>
    public SimpleObservableCollection(Action<T> onAdd, Action<T> onRemove)
    {
        this.onAdd = onAdd ?? throw new ArgumentNullException(nameof(onAdd));
        this.onRemove = onRemove ?? throw new ArgumentNullException(nameof(onRemove));
    }

    /// <inheritdoc/>
    protected override void ClearItems()
    {
        if (onRemove != null)
        {
            foreach (T val in this)
            {
                onRemove(val);
            }
        }

        base.ClearItems();
    }

    /// <inheritdoc/>
    protected override void InsertItem(int index, T item)
    {
        onAdd?.Invoke(item);
        
        base.InsertItem(index, item);
    }

    /// <inheritdoc/>
    protected override void RemoveItem(int index)
    {
        onRemove?.Invoke(this[index]);

        base.RemoveItem(index);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Removes the old item on failure to add a new one
    /// since onRemove has been already called for it.
    /// That said, it will still throw on failure to add.
    /// </remarks>
    protected override void SetItem(int index, T item)
    {
        onRemove?.Invoke(this[index]);

        try
        {
            onAdd?.Invoke(item);
        }
        catch
        {
            RemoveAt(index);
            
            throw;
        }

        base.SetItem(index, item);
    }
}
