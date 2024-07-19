using System;

namespace Stellar.WPF.Document;

/// <summary>
/// Describes a change of the document text. Thread-safe.
/// </summary>
[Serializable]
public class ChangeEventArgs : EventArgs
{
    private readonly int offset;
    private readonly ISource removedText;
    private readonly ISource insertedText;

    /// <summary>
    /// The offset at which the change occurs.
    /// </summary>
    public int Offset => offset;

    /// <summary>
    /// The removed text.
    /// </summary>
    public ISource RemovedText => removedText;

    /// <summary>
    /// The number of characters removed.
    /// </summary>
    public int RemovalLength => removedText.TextLength;

    /// <summary>
    /// The inserted text.
    /// </summary>
    public ISource InsertedText => insertedText;

    /// <summary>
    /// The number of characters inserted.
    /// </summary>
    public int InsertionLength => insertedText.TextLength;

    /// <summary>
    /// Constructor.
    /// </summary>
    public ChangeEventArgs(int offset, string removedText, string insertedText)
    {
        this.offset = offset < 0
            ? throw new ArgumentOutOfRangeException(nameof(offset), offset, "offset cannot be negative")
            : offset;

        this.removedText = removedText != null ? new StringSource(removedText) : StringSource.Empty;
        this.insertedText = insertedText != null ? new StringSource(insertedText) : StringSource.Empty;
    }

    /// <summary>
    /// Creates a new TextChangeEventArgs object.
    /// </summary>
    public ChangeEventArgs(int offset, ISource removedText, ISource insertedText)
    {
        this.offset = offset < 0
            ? throw new ArgumentOutOfRangeException(nameof(offset), offset, "offset cannot be negative")
            : offset;

        this.removedText = removedText ?? StringSource.Empty;
        this.insertedText = insertedText ?? StringSource.Empty;
    }

    /// <summary>
    /// Gets the new offset where the specified offset moves after this document change.
    /// </summary>
    public virtual int ComputeOffset(int offset, AnchorMovementType movementType = AnchorMovementType.Default)
    {
        if (Offset <= offset && offset <= Offset + RemovalLength)
        {
            return Offset + (movementType == AnchorMovementType.BeforeInsertion
                ? InsertionLength
                : 0);
        }

        return offset + (offset > Offset
            ? InsertionLength - RemovalLength
            : 0);
    }

    /// <summary>
    /// Creates TextChangeEventArgs for the reverse change.
    /// </summary>
    public virtual ChangeEventArgs Invert() => new(offset, insertedText, removedText);
}
