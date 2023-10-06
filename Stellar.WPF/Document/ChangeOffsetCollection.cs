using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Stellar.WPF.Document;

/// <summary>
/// Describes a series of offset changes.
/// </summary>
[Serializable]
public sealed class ChangeOffsetCollection : Collection<ChangeOffset>
{
    /// <summary>
    /// Immutable, empty OffsetChanges instance.
    /// </summary>
    public static readonly ChangeOffsetCollection Empty = new(Array.Empty<ChangeOffset>(), true);

    /// <summary>
    /// Whether changes are allowed.
    /// </summary>
    private bool isFrozen;

    /// <summary>
    /// Gets if this instance is frozen. Frozen instances are immutable and thus thread-safe.
    /// </summary>
    public bool IsFrozen => isFrozen;

    #region constructors
    public ChangeOffsetCollection()
    {
    }

    /// <summary>
    /// Creates a new frozen OffsetChanges instance with a single change.
    /// </summary>
    public ChangeOffsetCollection(ChangeOffset change)
        : this(new ChangeOffset[] { change }, true)
    {
    }

    internal ChangeOffsetCollection(int capacity)
        : base(new List<ChangeOffset>(capacity))
    {
    }

    private ChangeOffsetCollection(IList<ChangeOffset> entries, bool isFrozen)
        : base(entries)
    {
        this.isFrozen = isFrozen;
    }
    #endregion

    #region methods
    /// <summary>
    /// Compute the offset where the passed-in offset moves after a change.
    /// </summary>
    public int ComputeOffset(int offset, AnchorMovementType movementType = AnchorMovementType.Default)
    {
        IList<ChangeOffset> items = Items;
        int count = items.Count;

        for (int i = 0; i < count; i++)
        {
            offset = items[i].ComputeOffset(offset, movementType);
        }

        return offset;
    }

    /// <summary>
    /// Whether the collection is a valid explanation for the proposed change.
    /// </summary>
    public bool IsValidForDocumentChange(int offset, int removalLength, int insertionLength)
    {
        var endOffset = offset + removalLength;

        foreach (var change in this)
        {
            if (change.Offset < offset || endOffset < change.Offset + change.RemovalLength)
            {
                return false;
            }

            endOffset += change.InsertionLength - change.RemovalLength;
        }

        // whether the total delta matches the proposal
        return endOffset == offset + insertionLength;
    }

    /// <summary>
    /// Returns an inverted collection for the undo operation.
    /// </summary>
    public ChangeOffsetCollection Invert()
    {
        if (this == Empty)
        {
            return this;
        }

        var result = new ChangeOffsetCollection(Count);

        for (int i = Count - 1; i >= 0; i--)
        {
            var change = this[i];

            // swap insertion and removal lengths
            result.Add(new ChangeOffset(change.Offset, change.InsertionLength, change.RemovalLength));
        }

        return result;
    }

    /// <summary>
    /// Disallow changes to a frozen collection.
    /// </summary>
    /// <exception cref="InvalidOperationException"></exception>
    private void ThrowIfFrozen()
    {
        if (isFrozen)
        {
            throw new InvalidOperationException("This instance is frozen and cannot be modified.");
        }
    }

    /// <summary>
    /// Freezes this instance.
    /// </summary>
    public void Freeze()
    {
        isFrozen = true;
    }
    #endregion

    #region Collection overrides
    /// <inheritdoc/>
    protected override void ClearItems()
    {
        ThrowIfFrozen();

        base.ClearItems();
    }

    /// <inheritdoc/>
    protected override void InsertItem(int index, ChangeOffset item)
    {
        ThrowIfFrozen();

        base.InsertItem(index, item);
    }

    /// <inheritdoc/>
    protected override void RemoveItem(int index)
    {
        ThrowIfFrozen();

        base.RemoveItem(index);
    }

    /// <inheritdoc/>
    protected override void SetItem(int index, ChangeOffset item)
    {
        ThrowIfFrozen();

        base.SetItem(index, item);
    }
    #endregion
}

