using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Design;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using Stellar.WPF.Utilities;

namespace Stellar.WPF.Document;

/// <summary>
/// This class is the main class of the text model. Basically, it is a <see cref="System.Text.StringBuilder"/> with events.
/// </summary>
/// <remarks>
/// <b>Thread safety:</b>
/// <inheritdoc cref="VerifyAccess"/>
/// <para>However, there is a single method that is thread-safe: <see cref="CreateSnapshot()"/> (and its overloads).</para>
/// </remarks>
public sealed class Document : IDocument, INotifyPropertyChanged
{
    #region Thread ownership
    private readonly object _lock = new();
    private Thread owner = Thread.CurrentThread;

    /// <summary>
    /// Verifies that the current thread is the documents owner thread.
    /// Throws an <see cref="InvalidOperationException"/> if the wrong thread accesses the TextDocument.
    /// </summary>
    /// <remarks>
    /// <para>The TextDocument class is not thread-safe. A document instance expects to have a single owner thread
    /// and will throw an <see cref="InvalidOperationException"/> when accessed from another thread.
    /// It is possible to change the owner thread using the <see cref="TransferOwnershipTo"/> method.</para>
    /// </remarks>
    public void VerifyAccess()
    {
        if (Thread.CurrentThread != owner)
        {
            throw new InvalidOperationException("TextDocument can be accessed only from the thread that owns it.");
        }
    }

    /// <summary>
    /// Transfers ownership of the document to another thread, e.g., to load
    /// a document on a background thread and transferring it to the UI thread
    /// for displaying it when done loading.
    /// </summary>
    /// <remarks>
    /// <inheritdoc cref="VerifyAccess"/>
    /// <para>
    /// When null, no thread can access the document--until one takes ownership.
    /// THe lock ensures only one thread succeeds in that case.
    /// </para>
    /// </remarks>
    public void TransferOwnershipTo(Thread newOwner)
    {
        lock (_lock)
        {
            if (owner is not null)
            {
                VerifyAccess();
            }

            owner = newOwner;
        }
    }
    #endregion

    #region fields
    private readonly AnchorTree anchorTree;
    private readonly LineTree lineTree;
    private readonly LineManager lineManager;
    private readonly Tree<char> rope;
    private readonly CheckpointProvider checkpointProvider = new();
    #endregion

    #region constructor
    /// <summary>
    /// Create an empty text document.
    /// </summary>
    public Document()
        : this(string.Empty)
    {
    }

    /// <summary>
    /// Create a new text document with the specified initial text.
    /// </summary>
    public Document(IEnumerable<char> initialText)
    {
        if (initialText == null)
        {
            throw new ArgumentNullException(nameof(initialText));
        }

        rope = new Tree<char>(initialText);
        lineTree = new LineTree(this);
        lineManager = new LineManager(lineTree, this);
        lineTrackers.CollectionChanged += delegate
        {
            lineManager.UpdateLineTrackers();
        };

        anchorTree = new AnchorTree(this);
        undoStack = new UndoStack();
        FireChangeEvents();
    }

    /// <summary>
    /// Create a new text document with the specified initial text.
    /// </summary>
    public Document(ITextSource initialText)
        : this(GetTextFromTextSource(initialText))
    {
    }

    // gets the text from a text source, directly retrieving the underlying rope where possible
    private static IEnumerable<char> GetTextFromTextSource(ITextSource textSource)
    {
        if (textSource == null)
        {
            throw new ArgumentNullException(nameof(textSource));
        }

        var rts = textSource as RopeTextSource;

        if (rts != null)
        {
            return rts.GetRope();
        }

        var doc = textSource as Document;

        return doc is not null
            ? doc.rope
            : textSource.Text;
    }
    #endregion

    #region Text
    private void ThrowIfRangeInvalid(int offset, int length)
    {
        if (offset < 0 || offset > rope.Length)
        {
            throw new ArgumentOutOfRangeException("offset", offset, "0 <= offset <= " + rope.Length.ToString(CultureInfo.InvariantCulture));
        }
        if (length < 0 || offset + length > rope.Length)
        {
            throw new ArgumentOutOfRangeException("length", length, "0 <= length, offset(" + offset + ")+length <= " + rope.Length.ToString(CultureInfo.InvariantCulture));
        }
    }

    /// <inheritdoc/>
    public string GetText(int offset, int length)
    {
        VerifyAccess();
        return rope.ToString(offset, length);
    }

    /// <summary>
    /// Retrieves the text for a portion of the document.
    /// </summary>
    public string GetText(ISegment segment)
    {
        if (segment == null)
        {
            throw new ArgumentNullException(nameof(segment));
        }

        return GetText(segment.Offset, segment.Length);
    }

    /// <inheritdoc/>
    public int IndexOf(char c, int startIndex, int count)
    {
        DebugVerifyAccess();
        return rope.IndexOf(c, startIndex, count);
    }

    /// <inheritdoc/>
    public int LastIndexOf(char c, int startIndex, int count)
    {
        DebugVerifyAccess();
        return rope.LastIndexOf(c, startIndex, count);
    }

    /// <inheritdoc/>
    public int IndexOfAny(char[] anyOf, int startIndex, int count)
    {
        DebugVerifyAccess(); // frequently called (NewLineFinder), so must be fast in release builds
        return rope.IndexOfAny(anyOf, startIndex, count);
    }

    /// <inheritdoc/>
    public int IndexOf(string searchText, int startIndex, int count, StringComparison comparisonType)
    {
        DebugVerifyAccess();
        return rope.IndexOf(searchText, startIndex, count, comparisonType);
    }

    /// <inheritdoc/>
    public int LastIndexOf(string searchText, int startIndex, int count, StringComparison comparisonType)
    {
        DebugVerifyAccess();
        return rope.LastIndexOf(searchText, startIndex, count, comparisonType);
    }

    /// <inheritdoc/>
    public char GetCharAt(int offset)
    {
        DebugVerifyAccess(); // frequently called, so must be fast in release builds
        return rope[offset];
    }

    private WeakReference cachedText;

    /// <summary>
    /// Gets/Sets the text of the whole document.
    /// </summary>
    public string Text
    {
        get
        {
            VerifyAccess();
            string completeText = cachedText != null ? (cachedText.Target as string) : null;
            if (completeText == null)
            {
                completeText = rope.ToString();
                cachedText = new WeakReference(completeText);
            }
            return completeText;
        }
        set
        {
            VerifyAccess();
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            Replace(0, rope.Length, value);
        }
    }

    /// <summary>
    /// This event is called after a group of changes is completed.
    /// </summary>
    /// <remarks><inheritdoc cref="Changing"/></remarks>
    public event EventHandler TextChanged;

    event EventHandler IDocument.ChangeCompleted
    {
        add { TextChanged += value; }
        remove { TextChanged -= value; }
    }

    /// <inheritdoc/>
    public int TextLength
    {
        get
        {
            VerifyAccess();
            return rope.Length;
        }
    }

    /// <summary>
    /// Is raised when one of the properties <see cref="Text"/>, <see cref="TextLength"/>, <see cref="LineCount"/>,
    /// <see cref="UndoStack"/> changes.
    /// </summary>
    /// <remarks><inheritdoc cref="Changing"/></remarks>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Is raised before the document changes.
    /// </summary>
    /// <remarks>
    /// <para>Here is the order in which events are raised during a document update:</para>
    /// <list type="bullet">
    /// <item><description><b><see cref="BeginUpdate">BeginUpdate()</see></b></description>
    ///   <list type="bullet">
    ///   <item><description>Start of change group (on undo stack)</description></item>
    ///   <item><description><see cref="UpdateStarted"/> event is raised</description></item>
    ///   </list></item>
    /// <item><description><b><see cref="Insert(int,string)">Insert()</see> / <see cref="Remove(int,int)">Remove()</see> / <see cref="Replace(int,int,string)">Replace()</see></b></description>
    ///   <list type="bullet">
    ///   <item><description><see cref="Changing"/> event is raised</description></item>
    ///   <item><description>The document is changed</description></item>
    ///   <item><description><see cref="Anchor.Deleted">TextAnchor.Deleted</see> event is raised if anchors were
    ///     in the deleted text portion</description></item>
    ///   <item><description><see cref="Changed"/> event is raised</description></item>
    ///   </list></item>
    /// <item><description><b><see cref="EndUpdate">EndUpdate()</see></b></description>
    ///   <list type="bullet">
    ///   <item><description><see cref="TextChanged"/> event is raised</description></item>
    ///   <item><description><see cref="PropertyChanged"/> event is raised (for the Text, TextLength, LineCount properties, in that order)</description></item>
    ///   <item><description>End of change group (on undo stack)</description></item>
    ///   <item><description><see cref="UpdateFinished"/> event is raised</description></item>
    ///   </list></item>
    /// </list>
    /// <para>
    /// If the insert/remove/replace methods are called without a call to <c>BeginUpdate()</c>,
    /// they will call <c>BeginUpdate()</c> and <c>EndUpdate()</c> to ensure no change happens outside of <c>UpdateStarted</c>/<c>UpdateFinished</c>.
    /// </para><para>
    /// There can be multiple document changes between the <c>BeginUpdate()</c> and <c>EndUpdate()</c> calls.
    /// In this case, the events associated with EndUpdate will be raised only once after the whole document update is done.
    /// </para><para>
    /// The <see cref="UndoStack"/> listens to the <c>UpdateStarted</c> and <c>UpdateFinished</c> events to group all changes into a single undo step.
    /// </para>
    /// </remarks>
    public event EventHandler<DocumentChangeEventArgs> Changing;

    // Unfortunately EventHandler<T> is invariant, so we have to use two separate events
    private event EventHandler<TextChangeEventArgs> textChanging;

    event EventHandler<TextChangeEventArgs> IDocument.TextChanging
    {
        add { textChanging += value; }
        remove { textChanging -= value; }
    }

    /// <summary>
    /// Is raised after the document has changed.
    /// </summary>
    /// <remarks><inheritdoc cref="Changing"/></remarks>
    public event EventHandler<DocumentChangeEventArgs> Changed;

    private event EventHandler<TextChangeEventArgs> textChanged;

    event EventHandler<TextChangeEventArgs> IDocument.TextChanged
    {
        add { textChanged += value; }
        remove { textChanged -= value; }
    }

    /// <summary>
    /// Creates a snapshot of the current text.
    /// </summary>
    /// <remarks>
    /// <para>This method returns an immutable snapshot of the document, and may be safely called even when
    /// the document's owner thread is concurrently modifying the document.
    /// </para><para>
    /// This special thread-safety guarantee is valid only for TextDocument.CreateSnapshot(), not necessarily for other
    /// classes implementing ITextSource.CreateSnapshot().
    /// </para><para>
    /// </para>
    /// </remarks>
    public ITextSource CreateSnapshot()
    {
        lock (_lock)
        {
            return new RopeTextSource(rope, checkpointProvider.Current);
        }
    }

    /// <summary>
    /// Creates a snapshot of a part of the current text.
    /// </summary>
    /// <remarks><inheritdoc cref="CreateSnapshot()"/></remarks>
    public ITextSource CreateSnapshot(int offset, int length)
    {
        lock (_lock)
        {
            return new RopeTextSource(rope.Slice(offset, length));
        }
    }

    /// <inheritdoc/>
    public ICheckpoint Checkpoint => checkpointProvider.Current;

    /// <inheritdoc/>
    public System.IO.TextReader CreateReader()
    {
        lock (_lock)
        {
            return new RopeTextReader(rope);
        }
    }

    /// <inheritdoc/>
    public System.IO.TextReader CreateReader(int offset, int length)
    {
        lock (_lock)
        {
            return new RopeTextReader(rope.Slice(offset, length));
        }
    }

    /// <inheritdoc/>
    public void WriteTextTo(System.IO.TextWriter writer)
    {
        VerifyAccess();
        rope.WriteTo(writer, 0, rope.Length);
    }

    /// <inheritdoc/>
    public void WriteTextTo(System.IO.TextWriter writer, int offset, int length)
    {
        VerifyAccess();
        rope.WriteTo(writer, offset, length);
    }
    #endregion

    #region BeginUpdate / EndUpdate
    private int beginUpdateCount;

    /// <summary>
    /// Gets if an update is running.
    /// </summary>
    /// <remarks><inheritdoc cref="BeginUpdate"/></remarks>
    public bool IsInUpdate
    {
        get
        {
            VerifyAccess();
            return beginUpdateCount > 0;
        }
    }

    /// <summary>
    /// Immediately calls <see cref="BeginUpdate()"/>,
    /// and returns an IDisposable that calls <see cref="EndUpdate()"/>.
    /// </summary>
    /// <remarks><inheritdoc cref="BeginUpdate"/></remarks>
    public IDisposable RunUpdate()
    {
        BeginUpdate();

        return new FirstCallDisposable(EndUpdate);
    }

    /// <summary>
    /// <para>Begins a group of document changes.</para>
    /// <para>Some events are suspended until EndUpdate is called, and the <see cref="UndoStack"/> will
    /// group all changes into a single action.</para>
    /// <para>Calling BeginUpdate several times increments a counter, only after the appropriate number
    /// of EndUpdate calls the events resume their work.</para>
    /// </summary>
    /// <remarks><inheritdoc cref="Changing"/></remarks>
    public void BeginUpdate()
    {
        VerifyAccess();
        if (inDocumentChanging)
        {
            throw new InvalidOperationException("Cannot change document within another document change.");
        }

        beginUpdateCount++;
        if (beginUpdateCount == 1)
        {
            undoStack.StartUndoGroup();
            if (UpdateStarted != null)
            {
                UpdateStarted(this, EventArgs.Empty);
            }
        }
    }

    /// <summary>
    /// Ends a group of document changes.
    /// </summary>
    /// <remarks><inheritdoc cref="Changing"/></remarks>
    public void EndUpdate()
    {
        VerifyAccess();
        if (inDocumentChanging)
        {
            throw new InvalidOperationException("Cannot end update within document change.");
        }

        if (beginUpdateCount == 0)
        {
            throw new InvalidOperationException("No update is active.");
        }

        if (beginUpdateCount == 1)
        {
            // fire change events inside the change group - event handlers might add additional
            // document changes to the change group
            FireChangeEvents();
            undoStack.EndUndoGroup();
            beginUpdateCount = 0;
            if (UpdateFinished != null)
            {
                UpdateFinished(this, EventArgs.Empty);
            }
        }
        else
        {
            beginUpdateCount -= 1;
        }
    }

    /// <summary>
    /// Occurs when a document change starts.
    /// </summary>
    /// <remarks><inheritdoc cref="Changing"/></remarks>
    public event EventHandler UpdateStarted;

    /// <summary>
    /// Occurs when a document change is finished.
    /// </summary>
    /// <remarks><inheritdoc cref="Changing"/></remarks>
    public event EventHandler UpdateFinished;

    void IDocument.StartUndoableAction()
    {
        BeginUpdate();
    }

    void IDocument.EndUndoableAction()
    {
        EndUpdate();
    }

    IDisposable IDocument.OpenUndoGroup()
    {
        return RunUpdate();
    }
    #endregion

    #region Fire events after update
    private int oldTextLength;
    private int oldLineCount;
    private bool fireTextChanged;

    /// <summary>
    /// Fires TextChanged, TextLengthChanged, LineCountChanged if required.
    /// </summary>
    internal void FireChangeEvents()
    {
        // it may be necessary to fire the event multiple times if the document is changed
        // from inside the event handlers
        while (fireTextChanged)
        {
            fireTextChanged = false;
            if (TextChanged != null)
            {
                TextChanged(this, EventArgs.Empty);
            }

            OnPropertyChanged(nameof(Text));

            int textLength = rope.Length;
            if (textLength != oldTextLength)
            {
                oldTextLength = textLength;
                OnPropertyChanged(nameof(TextLength));
            }
            int lineCount = lineTree.LineCount;
            if (lineCount != oldLineCount)
            {
                oldLineCount = lineCount;
                OnPropertyChanged(nameof(LineCount));
            }
        }
    }

    private void OnPropertyChanged(string propertyName)
    {
        if (PropertyChanged != null)
        {
            PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }
    }
    #endregion

    #region Insert / Remove  / Replace
    /// <summary>
    /// Inserts text.
    /// </summary>
    /// <param name="offset">The offset at which the text is inserted.</param>
    /// <param name="text">The new text.</param>
    /// <remarks>
    /// Anchors positioned exactly at the insertion offset will move according to their movement type.
    /// For AnchorMovementType.Default, they will move behind the inserted text.
    /// The caret will also move behind the inserted text.
    /// </remarks>
    public void Insert(int offset, string text)
    {
        Replace(offset, 0, new StringTextSource(text), null);
    }

    /// <summary>
    /// Inserts text.
    /// </summary>
    /// <param name="offset">The offset at which the text is inserted.</param>
    /// <param name="text">The new text.</param>
    /// <remarks>
    /// Anchors positioned exactly at the insertion offset will move according to their movement type.
    /// For AnchorMovementType.Default, they will move behind the inserted text.
    /// The caret will also move behind the inserted text.
    /// </remarks>
    public void Insert(int offset, ITextSource text)
    {
        Replace(offset, 0, text, null);
    }

    /// <summary>
    /// Inserts text.
    /// </summary>
    /// <param name="offset">The offset at which the text is inserted.</param>
    /// <param name="text">The new text.</param>
    /// <param name="defaultAnchorMovementType">
    /// Anchors positioned exactly at the insertion offset will move according to the anchor's movement type.
    /// For AnchorMovementType.Default, they will move according to the movement type specified by this parameter.
    /// The caret will also move according to the <paramref name="defaultAnchorMovementType"/> parameter.
    /// </param>
    public void Insert(int offset, string text, AnchorMovementType defaultAnchorMovementType)
    {
        if (defaultAnchorMovementType == AnchorMovementType.BeforeInsertion)
        {
            Replace(offset, 0, new StringTextSource(text), ChangeOffsetType.KeepAnchorsInFront);
        }
        else
        {
            Replace(offset, 0, new StringTextSource(text), null);
        }
    }

    /// <summary>
    /// Inserts text.
    /// </summary>
    /// <param name="offset">The offset at which the text is inserted.</param>
    /// <param name="text">The new text.</param>
    /// <param name="defaultAnchorMovementType">
    /// Anchors positioned exactly at the insertion offset will move according to the anchor's movement type.
    /// For AnchorMovementType.Default, they will move according to the movement type specified by this parameter.
    /// The caret will also move according to the <paramref name="defaultAnchorMovementType"/> parameter.
    /// </param>
    public void Insert(int offset, ITextSource text, AnchorMovementType defaultAnchorMovementType)
    {
        if (defaultAnchorMovementType == AnchorMovementType.BeforeInsertion)
        {
            Replace(offset, 0, text, ChangeOffsetType.KeepAnchorsInFront);
        }
        else
        {
            Replace(offset, 0, text, null);
        }
    }

    /// <summary>
    /// Removes text.
    /// </summary>
    public void Remove(ISegment segment)
    {
        Replace(segment, string.Empty);
    }

    /// <summary>
    /// Removes text.
    /// </summary>
    /// <param name="offset">Starting offset of the text to be removed.</param>
    /// <param name="length">Length of the text to be removed.</param>
    public void Remove(int offset, int length)
    {
        Replace(offset, length, StringTextSource.Empty);
    }

    internal bool inDocumentChanging;

    /// <summary>
    /// Replaces text.
    /// </summary>
    public void Replace(ISegment segment, string text)
    {
        if (segment == null)
        {
            throw new ArgumentNullException(nameof(segment));
        }

        Replace(segment.Offset, segment.Length, new StringTextSource(text), null);
    }

    /// <summary>
    /// Replaces text.
    /// </summary>
    public void Replace(ISegment segment, ITextSource text)
    {
        if (segment == null)
        {
            throw new ArgumentNullException(nameof(segment));
        }

        Replace(segment.Offset, segment.Length, text, null);
    }

    /// <summary>
    /// Replaces text.
    /// </summary>
    /// <param name="offset">The starting offset of the text to be replaced.</param>
    /// <param name="length">The length of the text to be replaced.</param>
    /// <param name="text">The new text.</param>
    public void Replace(int offset, int length, string text)
    {
        Replace(offset, length, new StringTextSource(text), null);
    }

    /// <summary>
    /// Replaces text.
    /// </summary>
    /// <param name="offset">The starting offset of the text to be replaced.</param>
    /// <param name="length">The length of the text to be replaced.</param>
    /// <param name="text">The new text.</param>
    public void Replace(int offset, int length, ITextSource text)
    {
        Replace(offset, length, text, null);
    }

    /// <summary>
    /// Replaces text.
    /// </summary>
    /// <param name="offset">The starting offset of the text to be replaced.</param>
    /// <param name="length">The length of the text to be replaced.</param>
    /// <param name="text">The new text.</param>
    /// <param name="offsetChangeMappingType">The offsetChangeMappingType determines how offsets inside the old text are mapped to the new text.
    /// This affects how the anchors and segments inside the replaced region behave.</param>
    public void Replace(int offset, int length, string text, ChangeOffsetType offsetChangeMappingType)
    {
        Replace(offset, length, new StringTextSource(text), offsetChangeMappingType);
    }

    /// <summary>
    /// Replaces text.
    /// </summary>
    /// <param name="offset">The starting offset of the text to be replaced.</param>
    /// <param name="length">The length of the text to be replaced.</param>
    /// <param name="text">The new text.</param>
    /// <param name="offsetChangeMappingType">The offsetChangeMappingType determines how offsets inside the old text are mapped to the new text.
    /// This affects how the anchors and segments inside the replaced region behave.</param>
    public void Replace(int offset, int length, ITextSource text, ChangeOffsetType offsetChangeMappingType)
    {
        if (text == null)
        {
            throw new ArgumentNullException(nameof(text));
        }
        // Please see OffsetChangeMappingType XML comments for details on how these modes work.
        switch (offsetChangeMappingType)
        {
            case ChangeOffsetType.Default:
                Replace(offset, length, text, null);
                break;
            case ChangeOffsetType.KeepAnchorsInFront:
                Replace(offset, length, text, new ChangeOffsetCollection(
                    new ChangeOffset(offset, length, text.TextLength, false, true)));
                break;
            case ChangeOffsetType.RemoveThenInsert:
                if (length == 0 || text.TextLength == 0)
                {
                    // only insertion or only removal?
                    // OffsetChangeMappingType doesn't matter, just use Normal.
                    Replace(offset, length, text, null);
                }
                else
                {
                    ChangeOffsetCollection map = new(2);
                    map.Add(new ChangeOffset(offset, length, 0));
                    map.Add(new ChangeOffset(offset, 0, text.TextLength));
                    map.Freeze();
                    Replace(offset, length, text, map);
                }
                break;
            case ChangeOffsetType.ReplaceCharacters:
                if (length == 0 || text.TextLength == 0)
                {
                    // only insertion or only removal?
                    // OffsetChangeMappingType doesn't matter, just use Normal.
                    Replace(offset, length, text, null);
                }
                else if (text.TextLength > length)
                {
                    // look at OffsetChangeMappingType.CharacterReplace XML comments on why we need to replace
                    // the last character
                    ChangeOffset entry = new(offset + length - 1, 1, 1 + text.TextLength - length);
                    Replace(offset, length, text, new ChangeOffsetCollection(entry));
                }
                else if (text.TextLength < length)
                {
                    ChangeOffset entry = new(offset + text.TextLength, length - text.TextLength, 0, true, false);
                    Replace(offset, length, text, new ChangeOffsetCollection(entry));
                }
                else
                {
                    Replace(offset, length, text, ChangeOffsetCollection.Empty);
                }
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(offsetChangeMappingType), offsetChangeMappingType, "Invalid enum value");
        }
    }

    /// <summary>
    /// Replaces text.
    /// </summary>
    /// <param name="offset">The starting offset of the text to be replaced.</param>
    /// <param name="length">The length of the text to be replaced.</param>
    /// <param name="text">The new text.</param>
    /// <param name="offsetChangeMap">The offsetChangeMap determines how offsets inside the old text are mapped to the new text.
    /// This affects how the anchors and segments inside the replaced region behave.
    /// If you pass null (the default when using one of the other overloads), the offsets are changed as
    /// in OffsetChangeMappingType.Normal mode.
    /// If you pass OffsetChangeMap.Empty, then everything will stay in its old place (OffsetChangeMappingType.CharacterReplace mode).
    /// The offsetChangeMap must be a valid 'explanation' for the document change. See <see cref="ChangeOffsetCollection.IsValidForDocumentChange"/>.
    /// Passing an OffsetChangeMap to the Replace method will automatically freeze it to ensure the thread safety of the resulting
    /// DocumentChangeEventArgs instance.
    /// </param>
    public void Replace(int offset, int length, string text, ChangeOffsetCollection offsetChangeMap)
    {
        Replace(offset, length, new StringTextSource(text), offsetChangeMap);
    }

    /// <summary>
    /// Replaces text.
    /// </summary>
    /// <param name="offset">The starting offset of the text to be replaced.</param>
    /// <param name="length">The length of the text to be replaced.</param>
    /// <param name="text">The new text.</param>
    /// <param name="offsetChangeMap">The offsetChangeMap determines how offsets inside the old text are mapped to the new text.
    /// This affects how the anchors and segments inside the replaced region behave.
    /// If you pass null (the default when using one of the other overloads), the offsets are changed as
    /// in OffsetChangeMappingType.Normal mode.
    /// If you pass OffsetChangeMap.Empty, then everything will stay in its old place (OffsetChangeMappingType.CharacterReplace mode).
    /// The offsetChangeMap must be a valid 'explanation' for the document change. See <see cref="ChangeOffsetCollection.IsValidForDocumentChange"/>.
    /// Passing an OffsetChangeMap to the Replace method will automatically freeze it to ensure the thread safety of the resulting
    /// DocumentChangeEventArgs instance.
    /// </param>
    public void Replace(int offset, int length, ITextSource text, ChangeOffsetCollection offsetChangeMap)
    {
        if (text == null)
        {
            throw new ArgumentNullException(nameof(text));
        }

        text = text.CreateSnapshot();
        if (offsetChangeMap != null)
        {
            offsetChangeMap.Freeze();
        }

        // Ensure that all changes take place inside an update group.
        // Will also take care of throwing an exception if inDocumentChanging is set.
        BeginUpdate();
        try
        {
            // protect document change against corruption by other changes inside the event handlers
            inDocumentChanging = true;
            try
            {
                // The range verification must wait until after the BeginUpdate() call because the document
                // might be modified inside the UpdateStarted event.
                ThrowIfRangeInvalid(offset, length);

                DoReplace(offset, length, text, offsetChangeMap);
            }
            finally
            {
                inDocumentChanging = false;
            }
        }
        finally
        {
            EndUpdate();
        }
    }

    private void DoReplace(int offset, int length, ITextSource newText, ChangeOffsetCollection offsetChangeMap)
    {
        if (length == 0 && newText.TextLength == 0)
        {
            return;
        }

        // trying to replace a single character in 'Normal' mode?
        // for single characters, 'CharacterReplace' mode is equivalent, but more performant
        // (we don't have to touch the anchorTree at all in 'CharacterReplace' mode)
        if (length == 1 && newText.TextLength == 1 && offsetChangeMap == null)
        {
            offsetChangeMap = ChangeOffsetCollection.Empty;
        }

        ITextSource removedText;
        if (length == 0)
        {
            removedText = StringTextSource.Empty;
        }
        else if (length < 100)
        {
            removedText = new StringTextSource(rope.ToString(offset, length));
        }
        else
        {
            // use a rope if the removed string is long
            removedText = new RopeTextSource(rope.Slice(offset, length));
        }
        DocumentChangeEventArgs args = new(offset, removedText, newText, offsetChangeMap);

        // fire DocumentChanging event
        if (Changing != null)
        {
            Changing(this, args);
        }

        if (textChanging != null)
        {
            textChanging(this, args);
        }

        undoStack.Push(this, args);

        cachedText = null; // reset cache of complete document text
        fireTextChanged = true;
        var eventQueue = new EventQueue();

        lock (_lock)
        {
            // create linked list of checkpoints
            checkpointProvider.Append(args);

            // now update the textBuffer and lineTree
            if (offset == 0 && length == rope.Length)
            {
                // optimize replacing the whole document
                rope.Clear();
                var newRopeTextSource = newText as RopeTextSource;
                if (newRopeTextSource != null)
                {
                    rope.InsertAt(0, newRopeTextSource.GetRope());
                }
                else
                {
                    rope.InsertText(0, newText.Text);
                }

                lineManager.Rebuild();
            }
            else
            {
                rope.RemoveAt(offset, length);
                lineManager.Remove(offset, length);
#if DEBUG
                lineTree.ValidateData();
#endif
                var newRopeTextSource = newText as RopeTextSource;
                if (newRopeTextSource != null)
                {
                    rope.InsertAt(offset, newRopeTextSource.GetRope());
                }
                else
                {
                    rope.InsertText(offset, newText.Text);
                }

                lineManager.Insert(offset, newText);
#if DEBUG
                lineTree.ValidateData();
#endif
            }
        }

        // update text anchors
        if (offsetChangeMap == null)
        {
            anchorTree.HandleTextChange(args.CreateChange(), eventQueue);
        }
        else
        {
            foreach (ChangeOffset entry in offsetChangeMap)
            {
                anchorTree.HandleTextChange(entry, eventQueue);
            }
        }

        lineManager.ChangeComplete(args);

        // raise delayed events after our data structures are consistent again
        eventQueue.Flush();

        // fire DocumentChanged event
        if (Changed != null)
        {
            Changed(this, args);
        }

        if (textChanged != null)
        {
            textChanged(this, args);
        }
    }
    #endregion

    #region GetLineBy...
    /// <summary>
    /// Gets a read-only list of lines.
    /// </summary>
    /// <remarks><inheritdoc cref="Line"/></remarks>
    public IList<Line> Lines => lineTree;

    /// <summary>
    /// Gets a line by the line number: O(log n)
    /// </summary>
    public Line GetLineByNumber(int number)
    {
        VerifyAccess();
        if (number < 1 || number > lineTree.LineCount)
        {
            throw new ArgumentOutOfRangeException(nameof(number), number, "Value must be between 1 and " + lineTree.LineCount);
        }

        return lineTree.LineAt(number - 1);
    }

    ILine IDocument.GetLineByNumber(int lineNumber)
    {
        return GetLineByNumber(lineNumber);
    }

    /// <summary>
    /// Gets a document lines by offset.
    /// Runtime: O(log n)
    /// </summary>
    public Line GetLineByOffset(int offset)
    {
        VerifyAccess();
        if (offset < 0 || offset > rope.Length)
        {
            throw new ArgumentOutOfRangeException("offset", offset, "0 <= offset <= " + rope.Length.ToString());
        }
        return lineTree.LineBy(offset);
    }

    ILine IDocument.GetLineByOffset(int offset)
    {
        return GetLineByOffset(offset);
    }
    #endregion

    #region GetOffset / GetLocation
    /// <summary>
    /// Gets the offset from a text location.
    /// </summary>
    /// <seealso cref="GetLocation"/>
    public int GetOffset(Location location)
    {
        return GetOffset(location.Line, location.Column);
    }

    /// <summary>
    /// Gets the offset from a text location.
    /// </summary>
    /// <seealso cref="GetLocation"/>
    public int GetOffset(int line, int column)
    {
        Line docLine = GetLineByNumber(line);
        if (column <= 0)
        {
            return docLine.Offset;
        }

        if (column > docLine.Length)
        {
            return docLine.EndOffset;
        }

        return docLine.Offset + column - 1;
    }

    /// <summary>
    /// Gets the location from an offset.
    /// </summary>
    /// <seealso cref="GetOffset(Location)"/>
    public Location GetLocation(int offset)
    {
        Line line = GetLineByOffset(offset);
        return new Location(line.LineNumber, offset - line.Offset + 1);
    }
    #endregion

    #region Line Trackers
    private readonly ObservableCollection<ILineTracker> lineTrackers = new();

    /// <summary>
    /// Gets the list of <see cref="ILineTracker"/>s attached to this document.
    /// You can add custom line trackers to this list.
    /// </summary>
    public IList<ILineTracker> LineTrackers
    {
        get
        {
            VerifyAccess();
            return lineTrackers;
        }
    }
    #endregion

    #region UndoStack
    private UndoStack undoStack;

    /// <summary>
    /// Gets the <see cref="UndoStack"/> of the document.
    /// </summary>
    /// <remarks>This property can also be used to set the undo stack, e.g. for sharing a common undo stack between multiple documents.</remarks>
    public UndoStack UndoStack
    {
        get { return undoStack; }
        set
        {
            if (value == null)
            {
                throw new ArgumentNullException();
            }

            if (value != undoStack)
            {
                undoStack.ClearAll(); // first clear old undo stack, so that it can't be used to perform unexpected changes on this document
                                      // ClearAll() will also throw an exception when it's not safe to replace the undo stack (e.g. update is currently in progress)
                undoStack = value;
                OnPropertyChanged(nameof(UndoStack));
            }
        }
    }
    #endregion

    #region CreateAnchor
    /// <summary>
    /// Creates a new <see cref="Anchor"/> at the specified offset.
    /// </summary>
    /// <inheritdoc cref="Anchor" select="remarks|example"/>
    public Anchor CreateAnchor(int offset)
    {
        VerifyAccess();

        if (offset < 0 || rope.Length < offset)
        {
            throw new ArgumentOutOfRangeException("offset", offset, "0 <= offset <= " + rope.Length.ToString(CultureInfo.InvariantCulture));
        }
        return anchorTree.CreateAnchor(offset);
    }

    ITextAnchor IDocument.CreateAnchor(int offset)
    {
        return CreateAnchor(offset);
    }
    #endregion

    #region LineCount
    /// <summary>
    /// Gets the total number of lines in the document.
    /// Runtime: O(1).
    /// </summary>
    public int LineCount
    {
        get
        {
            VerifyAccess();
            return lineTree.LineCount;
        }
    }

    #endregion

    #region Debugging
    [Conditional("DEBUG")]
    internal void DebugVerifyAccess()
    {
        VerifyAccess();
    }

    /// <summary>
    /// Gets the document lines tree in string form.
    /// </summary>
    internal string GetLineTreeAsString()
    {
#if DEBUG
        return lineTree.GetTreeAsString();
#else
			return "Not available in release build.";
#endif
    }

    /// <summary>
    /// Gets the text anchor tree in string form.
    /// </summary>
    internal string GetTextAnchorTreeAsString()
    {
#if DEBUG
        return anchorTree.GetTreeAsString();
#else
			return "Not available in release build.";
#endif
    }
    #endregion

    #region Service Provider
    private IServiceProvider serviceProvider;

    /// <summary>
    /// Gets/Sets the service provider associated with this document.
    /// By default, every TextDocument has its own ServiceContainer; and has the document itself
    /// registered as <see cref="IDocument"/> and <see cref="Document"/>.
    /// </summary>
    public IServiceProvider ServiceProvider
    {
        get
        {
            VerifyAccess();
            if (serviceProvider == null)
            {
                var container = new ServiceContainer();
                container.AddService(typeof(IDocument), this);
                container.AddService(typeof(Document), this);
                serviceProvider = container;
            }
            return serviceProvider;
        }
        set
        {
            VerifyAccess();
            if (value == null)
            {
                throw new ArgumentNullException();
            }

            serviceProvider = value;
        }
    }

    object IServiceProvider.GetService(Type serviceType)
    {
        return ServiceProvider.GetService(serviceType);
    }
    #endregion

    #region FileName
    private string fileName;

    /// <inheritdoc/>
    public event EventHandler FileNameChanged;

    private void OnFileNameChanged(EventArgs e)
    {
        EventHandler handler = FileNameChanged;
        if (handler != null)
        {
            handler(this, e);
        }
    }

    /// <inheritdoc/>
    public string FileName
    {
        get { return fileName; }
        set
        {
            if (fileName != value)
            {
                fileName = value;
                OnFileNameChanged(EventArgs.Empty);
            }
        }
    }
    #endregion
}
