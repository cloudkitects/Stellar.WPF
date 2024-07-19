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
    /// Compares checkpoint ids, which are assigned in squence.
    /// </summary>
    /// <remarks>Thread-safe.</remarks>
    /// <exception cref="ArgumentException">Raised if other belongs to a different document.</exception>
    /// <returns>0 if <c>this</c> and <paramref name="other"/> represent the same checkpoint,
    /// -1 if this checkpoint is "newer" than <paramref name="other"/>, 1 if older and so on.</returns>
    int GetDistanceTo(ICheckpoint other);

    /// <summary>
    /// Gets the changes between this and the other checkpoint--reverse
    /// changes if <paramref name="other"/> is older.
    /// </summary>
    /// <remarks>Thread-safe.</remarks>
    /// <exception cref="ArgumentException">Raised if other belongs to a different document than this checkpoint.</exception>
    IEnumerable<ChangeEventArgs> GetChangesUpTo(ICheckpoint other);

    /// <summary>
    /// Calculates where the offset has moved in the other checkpoint.
    /// </summary>
    /// <exception cref="ArgumentException">Raised if other belongs to a different document than this checkpoint.</exception>
    int GetOffsetTo(ICheckpoint other, int oldOffset, AnchorMovementType movement = AnchorMovementType.Default);
}
