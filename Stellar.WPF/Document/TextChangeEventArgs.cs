using System;

namespace Stellar.WPF.Document;

/// <summary>
/// Describes a change of the document text. Thread-safe.
/// </summary>
[Serializable]
public class TextChangeEventArgs : EventArgs
{
    private readonly int offset;
    private readonly ITextSource removedText;
    private readonly ITextSource insertedText;

    /// <summary>
    /// The offset at which the change occurs.
    /// </summary>
    public int Offset => offset;

    /// <summary>
    /// The removed text.
    /// </summary>
    public ITextSource RemovedText => removedText;

    /// <summary>
    /// The number of characters removed.
    /// </summary>
    public int RemovalLength => removedText.TextLength;

    /// <summary>
    /// The inserted text.
    /// </summary>
    public ITextSource InsertedText => insertedText;

    /// <summary>
    /// The number of characters inserted.
    /// </summary>
    public int InsertionLength => insertedText.TextLength;

    /// <summary>
    /// Constructor.
    /// </summary>
    public TextChangeEventArgs(int offset, string removedText, string insertedText)
    {
        this.offset = offset < 0
            ? throw new ArgumentOutOfRangeException(nameof(offset), offset, "offset cannot be negative")
            : offset;

        this.removedText = removedText != null ? new StringTextSource(removedText) : StringTextSource.Empty;
        this.insertedText = insertedText != null ? new StringTextSource(insertedText) : StringTextSource.Empty;
    }

    /// <summary>
    /// Creates a new TextChangeEventArgs object.
    /// </summary>
    public TextChangeEventArgs(int offset, ITextSource removedText, ITextSource insertedText)
    {
        this.offset = offset < 0
            ? throw new ArgumentOutOfRangeException(nameof(offset), offset, "offset cannot be negative")
            : offset;

        this.removedText = removedText ?? StringTextSource.Empty;
        this.insertedText = insertedText ?? StringTextSource.Empty;
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
    public virtual TextChangeEventArgs Invert() => new(offset, insertedText, removedText);
}
