using System;

namespace Stellar.WPF.Document;

/// <summary>
/// The offset (point insertion or removal) of a document change.
/// </summary>
[Serializable]
public readonly struct ChangeOffset : IEquatable<ChangeOffset>
{
    private readonly int offset;

    // MSB: DefaultAnchorMovementIsBeforeInsertion
    private readonly uint insertionLengthWithMovementFlag;

    // MSB: RemovalNeverCausesAnchorDeletion; other 31 bits: RemovalLength
    private readonly uint removalLengthWithDeletionFlag;

    /// <summary>
    /// The offset at which the change occurs.
    /// </summary>
    public int Offset => offset;

    /// <summary>
    /// The number of characters inserted, 0 if this entry represents a removal.
    /// </summary>
    public int InsertionLength => (int)(insertionLengthWithMovementFlag & 0x7fffffff);

    /// <summary>
    /// The number of characters removed, 0 if this entry represents an insertion.
    /// </summary>
    public int RemovalLength => (int)(removalLengthWithDeletionFlag & 0x7fffffff);

    /// <summary>
    /// Whether the removal should not cause any anchor deletions.
    /// </summary>
    public bool RemovalNeverCausesAnchorDeletion => (removalLengthWithDeletionFlag & 0x80000000) != 0;

    /// <summary>
    /// Whether default anchor movement causes the anchor to stay in front of the caret.
    /// </summary>
    public bool DefaultAnchorMovementIsBeforeInsertion => (insertionLengthWithMovementFlag & 0x80000000) != 0;

    #region constructors
    /// <summary>
    /// Creates a new OffsetChangeMapEntry instance.
    /// </summary>
    public ChangeOffset(int offset, int removalLength, int insertionLength)
    {
        this.offset = (int)(uint)offset;
        removalLengthWithDeletionFlag = (uint)removalLength;
        insertionLengthWithMovementFlag = (uint)insertionLength;
    }

    /// <summary>
    /// Creates a new OffsetChangeMapEntry instance.
    /// </summary>
    public ChangeOffset(int offset, int removalLength, int insertionLength, bool removalNeverCausesAnchorDeletion, bool defaultAnchorMovementIsBeforeInsertion)
        : this(offset, removalLength, insertionLength)
    {
        if (removalNeverCausesAnchorDeletion)
        {
            removalLengthWithDeletionFlag |= 0x80000000;
        }

        if (defaultAnchorMovementIsBeforeInsertion)
        {
            insertionLengthWithMovementFlag |= 0x80000000;
        }
    }
    #endregion

    #region methods
    /// <summary>
    /// The new offset after the change.
    /// </summary>
    public int ComputeOffset(int oldOffset, AnchorMovementType movementType = AnchorMovementType.Default)
    {
        var insertionLength = InsertionLength;
        var removalLength = RemovalLength;

        if (!(removalLength == 0 && oldOffset == offset))
        {
            // both conditions below can be true: an insert at the offset without removal.
            // We'd need to disambiguate by movement type, but that's handled later

            // offset is before start of change: no movement
            if (oldOffset <= offset)
            {
                return oldOffset;
            }

            // offset is after end of change: movement by normal delta
            if (oldOffset >= offset + removalLength)
            {
                return oldOffset + insertionLength - removalLength;
            }
        }

        // either the old offset is inside the deleted segment or there was
        // an insert at the caret without removal
        if (movementType == AnchorMovementType.AfterInsertion)
        {
            return offset + insertionLength;
        }

        if (movementType == AnchorMovementType.BeforeInsertion || DefaultAnchorMovementIsBeforeInsertion)
        {
            return offset;
        }

        return offset + insertionLength;
    }
    #endregion

    #region IEquatable
    /// <inheritdoc/>
    public override int GetHashCode()
    {
        unchecked
        {
            return offset + 3559 * (int)insertionLengthWithMovementFlag + 3571 * (int)removalLengthWithDeletionFlag;
        }
    }

    /// <inheritdoc/>
    public override readonly bool Equals(object? obj) => obj is ChangeOffset entry && Equals(entry);

    /// <inheritdoc/>
    public readonly bool Equals(ChangeOffset other)
    {
        return offset == other.offset &&
            insertionLengthWithMovementFlag == other.insertionLengthWithMovementFlag &&
            removalLengthWithDeletionFlag == other.removalLengthWithDeletionFlag;
    }

    /// <summary>
    /// Tests the two entries for equality.
    /// </summary>
    public static bool operator ==(ChangeOffset left, ChangeOffset right)
    {
        return left.Equals(right);
    }

    /// <summary>
    /// Tests the two entries for inequality.
    /// </summary>
    public static bool operator !=(ChangeOffset left, ChangeOffset right)
    {
        return !left.Equals(right);
    }
    #endregion
}
