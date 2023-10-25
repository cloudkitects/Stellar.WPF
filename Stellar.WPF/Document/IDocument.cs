using System;

namespace Stellar.WPF.Document;

/// <summary>
/// A file to be edited.
/// </summary>
/// <remarks>
/// Line and column counting starts at 1 whereas offset starts at 0.
/// Modifying the document within event handlers or aborting the change (by throwing
/// an exception) are likely to cause corruption of data structures that listen to the
/// Changing and Changed events.
/// </remarks>
public interface IDocument : ITextSource, IServiceProvider
{
    /// <summary>
    /// Gets/Sets the text of the whole document.
    /// </summary>
    /// <remarks>It hides ITextSource.Text to add the setter.</remarks>
    new string Text { get; set; }

    /// <summary>
    /// Event called directly before a change is applied to the document.
    /// </summary>
    event EventHandler<TextChangeEventArgs> TextChanging;

    /// <summary>
    /// Event called directly after a change is applied to the document.
    /// </summary>
    event EventHandler<TextChangeEventArgs> TextChanged;

    /// <summary>
    /// Event called after a group of changes is completed.
    /// </summary>
    /// <seealso cref="EndUndoableAction"/>
    event EventHandler ChangeCompleted;

    /// <summary>
    /// Gets the number of lines in the document.
    /// </summary>
    int LineCount { get; }

    /// <summary>
    /// The document line with the specified number.
    /// </summary>
    /// <param name="lineNumber">The number of the line to retrieve. The first line has number 1.</param>
    ILine GetLineByNumber(int lineNumber);

    /// <summary>
    /// The document line that contains the specified offset.
    /// </summary>
    ILine GetLineByOffset(int offset);

    /// <summary>
    /// The offset for a given line and column.
    /// </summary>
    /// <seealso cref="GetLocation"/>
    int GetOffset(int line, int column);

    /// <summary>
    /// The offset for a given text location.
    /// </summary>
    /// <seealso cref="GetLocation"/>
    int GetOffset(Location location);

    /// <summary>
    /// The location from the given offset.
    /// </summary>
    /// <seealso cref="GetOffset(Location)"/>
    Location GetLocation(int offset);

    /// <summary>
    /// Inserts text.
    /// </summary>
    /// <param name="offset">The offset at which to insert the text.</param>
    /// <param name="text">The text to insert.</param>
    /// <remarks>
    /// Anchors positioned exactly at the insertion offset will move according to their movement type.
    /// For AnchorMovementType.Default, they will move behind the inserted text.
    /// The caret will also move behind the inserted text.
    /// </remarks>
    void Insert(int offset, string text);

    /// <summary>
    /// Inserts text.
    /// </summary>
    /// <param name="offset">The offset at which to insert the text.</param>
    /// <param name="text">The text to insert.</param>
    /// <remarks>
    /// Anchors positioned exactly at the insertion offset will move according to their movement type.
    /// For AnchorMovementType.Default, they will move behind the inserted text.
    /// The caret will also move behind the inserted text.
    /// </remarks>
    void Insert(int offset, ITextSource text);

    /// <summary>
    /// Inserts text.
    /// </summary>
    /// <param name="offset">The offset at which the text is inserted.</param>
    /// <param name="text">The new text.</param>
    /// <param name="defaultAnchorMovementType">
    /// Anchors positioned exactly at the insertion offset will move according to the anchor's movement type.
    /// For AnchorMovementType.Default, they will move according to the specified movement type.
    /// The caret will also move accordingly.
    /// </param>
    void Insert(int offset, string text, AnchorMovementType defaultAnchorMovementType);

    /// <summary>
    /// Inserts text.
    /// </summary>
    /// <param name="offset">The offset at which the text is inserted.</param>
    /// <param name="text">The new text.</param>
    /// <param name="defaultAnchorMovementType">
    /// Anchors positioned exactly at the insertion offset will move according to the anchor's movement type.
    /// For AnchorMovementType.Default, they will move according to the specified movement type.
    /// The caret will also move accordingly.
    /// </param>
    void Insert(int offset, ITextSource text, AnchorMovementType defaultAnchorMovementType);

    /// <summary>
    /// Removes text.
    /// </summary>
    /// <param name="offset">Starting offset of the text to be removed.</param>
    /// <param name="length">Length of the text to be removed.</param>
    void Remove(int offset, int length);

    /// <summary>
    /// Replaces text.
    /// </summary>
    /// <param name="offset">Starting offset of the text to be replaced.</param>
    /// <param name="length">The length of the text to be replaced.</param>
    /// <param name="newText">The new text.</param>
    void Replace(int offset, int length, string newText);

    /// <summary>
    /// Replaces text.
    /// </summary>
    /// <param name="offset">Starting offset of the text to be replaced.</param>
    /// <param name="length">The length of the text to be replaced.</param>
    /// <param name="newText">The new text.</param>
    void Replace(int offset, int length, ITextSource newText);

    /// <summary>
    /// Combine the following actions into a single one for undo purposes.
    /// </summary>
    void StartUndoableAction();

    /// <summary>
    /// Ends the undoable action started with <see cref="StartUndoableAction"/>.
    /// </summary>
    void EndUndoableAction();

    /// <summary>
    /// Creates an undo group. Dispose the returned value to close the undo group.
    /// </summary>
    /// <returns>An object that closes the undo group when Dispose() is called.</returns>
    IDisposable OpenUndoGroup();

    /// <summary>
    /// Creates a new <see cref="IAnchor"/> at the specified offset.
    /// </summary>
    /// <inheritdoc cref="IAnchor" select="remarks|example"/>
    IAnchor CreateAnchor(int offset);

    /// <summary>
    /// Gets the name of the file the document is stored in.
    /// Could be a non-existent dummy file name or null if no name has been set.
    /// </summary>
    string FileName { get; }

    /// <summary>
    /// Fired when the file name of the document changes.
    /// </summary>
    event EventHandler FileNameChanged;
}
