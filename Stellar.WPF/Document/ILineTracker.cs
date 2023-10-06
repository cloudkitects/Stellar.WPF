namespace Stellar.WPF.Document;

/// <summary>
/// Low-level line tracker.
/// </summary>
/// <remarks>
/// Implemented by LineManager immediately after the document changes and *while* the line tree is updating,
/// meaning the latter could be in an invalid state.
/// This interface should only be used to update per-line data structures like the height tree.
/// Line trackers must not raise any events during an update to prevent other code from seeing
/// the invalid state, but may be called while the text document has taken a lock, which means
/// care must be taken not to dead-lock inside their callbacks.
/// </remarks>
public interface ILineTracker
{
    /// <summary>
    /// Actions to execute before a line is removed from a document or a documet tree.
    /// </summary>
    void BeforeRemoving(Line line);

    /// <summary>
    /// Action to be called after the line length changes--whether it remains the same--
    /// and multiple times for a single line: a replacement is a removal followed by insertion.
    /// </summary>
    void ResetLength(Line line, int newTotalLength);

    /// <summary>
    /// Action after inserting a line.
    /// </summary>
    void AfterInserting(Line insertionPos, Line newLine);

    /// <summary>
    /// Must be called after the document changes and is in a consistent
    /// state. Line trackers should throw away their data and be rebuilt.
    /// </summary>
    void Rebuild();

    /// <summary>
    /// Notifies the line tracker that a document change (a single change, not a change group) has completed.
    /// This method gets called after the change has been performed, but before the <see cref="Document.Changed"/> event
    /// is raised.
    /// </summary>
    void AfterChange(DocumentChangeEventArgs e);
}
