using System;
using System.Collections.Generic;

namespace Stellar.WPF.Document;

/// <summary>
/// A text source's checkpoint.
/// </summary>
/// <remarks>
/// Checkpoints keep track of document changes to reparse 
/// or implement incremental parsers. Separation from ITextSource allows the
/// GC to collect the text source while the checkpoint is still in use.
/// </remarks>
public interface ICheckpoint
{
    /// <summary>
    /// Whether this checkpoint belongs to the same document as the other checkpoint.
    /// </summary>
    /// <remarks>
    /// Returns false when other is <c>null</c>.
    /// </remarks>
    bool BelongsToSameDocumentAs(ICheckpoint other);

    /// <summary>
    /// Compares the age of this checkpoint to the other checkpoint.
    /// </summary>
    /// <remarks>This method is thread-safe.</remarks>
    /// <exception cref="ArgumentException">Raised if other belongs to a different document.</exception>
    /// <returns>-1 if this checkpoint is older than <paramref name="other"/>.
    /// 0 if <c>this</c> and <paramref name="other"/> represent the same checkpoint.
    /// 1 if this checkpoint is newer than <paramref name="other"/>.</returns>
    int CompareAge(ICheckpoint other);

    /// <summary>
    /// Gets the changes from this checkpoint to the other checkpoint, or
    /// calculate reverse changes if <paramref name="other"/> is older.
    /// </summary>
    /// <remarks>This method is thread-safe.</remarks>
    /// <exception cref="ArgumentException">Raised if other belongs to a different document than this checkpoint.</exception>
    IEnumerable<TextChangeEventArgs> GetChangesTo(ICheckpoint other);

    /// <summary>
    /// Calculates where the offset has moved in the other checkpoint.
    /// </summary>
    /// <exception cref="ArgumentException">Raised if other belongs to a different document than this checkpoint.</exception>
    int MoveOffsetTo(ICheckpoint other, int oldOffset, AnchorMovementType movement = AnchorMovementType.Default);
}
