using System;

namespace Stellar.WPF.Document;

/// <summary>
/// Describes a change of the document text.
/// This class is thread-safe.
/// </summary>
[Serializable]
public class DocumentChangeEventArgs : TextChangeEventArgs
{
    private volatile ChangeOffsetCollection offsetChanges;

    /// <summary>
    /// Gets the OffsetChangeMap associated with this document change.
    /// </summary>
    /// <remarks>The OffsetChangeMap instance is guaranteed to be frozen and thus thread-safe.</remarks>
    public ChangeOffsetCollection OffsetChanges
    {
        get
        {
            ChangeOffsetCollection changes = offsetChanges;

            if (changes is null)
            {
                changes = new ChangeOffsetCollection(CreateChange());

                offsetChanges = changes;
            }

            return changes;
        }
    }

    internal ChangeOffset CreateChange()
    {
        return new ChangeOffset(Offset, RemovalLength, InsertionLength);
    }

    /// <summary>
    /// Shortcut to offsetChanges or null if the default (single replacement) offset changes collection is being used.
    /// </summary>
    internal ChangeOffsetCollection OffsetChangesOrNull => offsetChanges;

    /// <summary>
    /// Compute the new offset where the specified offset moves after this document change.
    /// </summary>
    public override int ComputeOffset(int offset, AnchorMovementType movementType = AnchorMovementType.Default)
    {
        return offsetChanges is null
            ? CreateChange().ComputeOffset(offset, movementType)
            : offsetChanges.ComputeOffset(offset, movementType);
    }

    /// <summary>
    /// Creates a new DocumentChangeEventArgs object.
    /// </summary>
    public DocumentChangeEventArgs(int offset, string removedText, string insertedText)
        : this(offset, removedText, insertedText, null)
    {
    }

    /// <summary>
    /// Creates a new DocumentChangeEventArgs object.
    /// </summary>
    public DocumentChangeEventArgs(int offset, string removedText, string insertedText, ChangeOffsetCollection changes)
        : base(offset, removedText, insertedText)
    {
        SetOffsetChanges(changes);
    }

    /// <summary>
    /// Creates a new DocumentChangeEventArgs object.
    /// </summary>
    public DocumentChangeEventArgs(int offset, ITextSource removedText, ITextSource insertedText, ChangeOffsetCollection changes)
        : base(offset, removedText, insertedText)
    {
        SetOffsetChanges(changes);
    }

    private void SetOffsetChanges(ChangeOffsetCollection changes)
    {
        if (changes is not null)
        {
            if (!changes.IsFrozen)
            {
                throw new ArgumentException("Offset changes collection must be frozen before using it in a change event");
            }

            if (!changes.IsValidForDocumentChange(Offset, RemovalLength, InsertionLength))
            {
                throw new ArgumentException("Offset changes is not valid for this change", nameof(changes));
            }

            offsetChanges = changes;
        }
    }

    /// <inheritdoc/>
    public override TextChangeEventArgs Invert()
    {
        var changes = OffsetChangesOrNull;

        if (changes is not null)
        {
            changes = changes.Invert();
            changes.Freeze();
        }

        return new DocumentChangeEventArgs(Offset, InsertedText, RemovedText, changes);
    }
}
