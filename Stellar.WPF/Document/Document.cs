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
    #region thread ownership
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
    /// When owner is null, no thread can access the document--until one takes ownership.
    /// THe lock ensures only one thread succeeds in that case.
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

    #region fields and props
    private readonly AnchorTree anchorTree;
    private readonly LineTree lineTree;
    private readonly LineManager lineManager;
    private readonly Tree<char> charTree;
    private readonly CheckpointProvider checkpointProvider = new();

    private WeakReference? cachedText;

    /// <summary>
    /// Gets/Sets the text of the whole document.
    /// </summary>
    public string Text
    {
        get
        {
            VerifyAccess();

            var completeText = cachedText is not null
                ? cachedText.Target as string
                : null;

            if (completeText is null)
            {
                completeText = charTree.ToString();

                cachedText = new WeakReference(completeText);
            }

            return completeText;
        }

        set
        {
            VerifyAccess();

            Replace(0, charTree.Length, value ?? throw new ArgumentNullException(nameof(value)));
        }
    }

    /// <inheritdoc/>
    public int TextLength
    {
        get
        {
            VerifyAccess();

            return charTree.Length;
        }
    }
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
    public Document(IEnumerable<char> text)
    {
        if (text is null)
        {
            throw new ArgumentNullException(nameof(text));
        }

        charTree = new Tree<char>(text);
        lineTree = new LineTree(this);
        lineManager = new LineManager(lineTree, this);
        
        lineTrackers.CollectionChanged += delegate { lineManager.UpdateLineTrackers(); };

        anchorTree = new AnchorTree(this);
        undoStack = new UndoStack();

        FireChangeEvents();
    }

    /// <summary>
    /// Create a new text document with the specified initial text.
    /// </summary>
    public Document(ITextSource text)
        : this(GetTextFromTextSource(text))
    {
    }

    // gets the text from a text source, directly retrieving the underlying rope where possible
    private static IEnumerable<char> GetTextFromTextSource(ITextSource textSource)
    {
        if (textSource == null)
        {
            throw new ArgumentNullException(nameof(textSource));
        }

        if (textSource is TextSource source)
        {
            return source.GetTree();
        }

        return textSource is Document document
            ? document.charTree
            : textSource.Text;
    }
    #endregion

    #region get text
    /// <inheritdoc/>
    public string GetText(int offset, int length)
    {
        VerifyAccess();

        return charTree.ToString(offset, length);
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
        
        return charTree.IndexOf(c, startIndex, count);
    }

    /// <inheritdoc/>
    public int LastIndexOf(char c, int startIndex, int count)
    {
        DebugVerifyAccess();
        
        return charTree.LastIndexOf(c, startIndex, count);
    }

    /// <inheritdoc/>
    public int IndexOfAny(char[] anyOf, int startIndex, int count)
    {
        DebugVerifyAccess();
        
        return charTree.IndexOfAny(anyOf, startIndex, count);
    }

    /// <inheritdoc/>
    public int IndexOf(string searchText, int startIndex, int count, StringComparison comparisonType)
    {
        DebugVerifyAccess();
        
        return charTree.IndexOf(searchText, startIndex, count, comparisonType);
    }

    /// <inheritdoc/>
    public int LastIndexOf(string searchText, int startIndex, int count, StringComparison comparisonType)
    {
        DebugVerifyAccess();
        
        return charTree.LastIndexOf(searchText, startIndex, count, comparisonType);
    }

    /// <inheritdoc/>
    public char GetCharAt(int offset)
    {
        DebugVerifyAccess();
        
        return charTree[offset];
    }

    #region events
    /// <summary>
    /// Event raised before the document changes.
    /// </summary>
    /// <remarks>
    /// <para>Events are raised in this order during a document change:</para>
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

    /// <summary>
    /// A separate event is required given EventHandler<T> is invariant.
    /// </summary>
    private event EventHandler<TextChangeEventArgs> textChanging;

    event EventHandler<TextChangeEventArgs> IDocument.TextChanging
    {
        add { textChanging += value; }
        remove { textChanging -= value; }
    }

    /// <summary>
    /// Event raised for completing a group of changes.
    /// </summary>
    event EventHandler IDocument.ChangeCompleted
    {
        add    { TextChanged += value; }
        remove { TextChanged -= value; }
    }

    /// <summary>
    /// Event raised after <see cref="Text"/>, <see cref="TextLength"/>, <see cref="LineCount"/>,
    /// or the <see cref="UndoStack"/> changes.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Is raised after the document has changed.
    /// </summary>
    /// <remarks><inheritdoc cref="Changing"/></remarks>
    public event EventHandler<DocumentChangeEventArgs> Changed;

    private event EventHandler<TextChangeEventArgs> textChanged;

    /// <summary>
    /// Event raised after <see cref="Text"/> property changed.
    /// </summary>
    public event EventHandler? TextChanged;

    event EventHandler<TextChangeEventArgs> IDocument.TextChanged
    {
        add { textChanged += value; }
        remove { textChanged -= value; }
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

            TextChanged?.Invoke(this, EventArgs.Empty);

            OnPropertyChanged(nameof(Text));

            var textLength = charTree.Length;

            if (textLength != oldTextLength)
            {
                oldTextLength = textLength;

                OnPropertyChanged(nameof(TextLength));
            }

            var lineCount = lineTree.LineCount;

            if (lineCount != oldLineCount)
            {
                oldLineCount = lineCount;

                OnPropertyChanged(nameof(LineCount));
            }
        }
    }

    private void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    #endregion

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
            return new TextSource(charTree, checkpointProvider.Current);
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
            return new TextSource(charTree.Slice(offset, length));
        }
    }

    /// <inheritdoc/>
    public ICheckpoint Checkpoint => checkpointProvider.Current;

    /// <inheritdoc/>
    public System.IO.TextReader CreateReader()
    {
        lock (_lock)
        {
            return new TreeTextReader(charTree);
        }
    }

    /// <inheritdoc/>
    public System.IO.TextReader CreateReader(int offset, int length)
    {
        lock (_lock)
        {
            return new TreeTextReader(charTree.Slice(offset, length));
        }
    }

    /// <inheritdoc/>
    public void WriteTextTo(System.IO.TextWriter writer)
    {
        VerifyAccess();
        charTree.WriteTo(writer, 0, charTree.Length);
    }

    /// <inheritdoc/>
    public void WriteTextTo(System.IO.TextWriter writer, int offset, int length)
    {
        VerifyAccess();
        charTree.WriteTo(writer, offset, length);
    }
    #endregion

    #region begin and end update
    private int updateCount;

    /// <summary>
    /// Whether an update is running.
    /// </summary>
    /// <remarks><inheritdoc cref="BeginUpdate"/></remarks>
    public bool IsUpdating
    {
        get
        {
            VerifyAccess();

            return updateCount > 0;
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
    public void BeginUpdate()
    {
        VerifyAccess();

        if (isDocumentChanging)
        {
            throw new InvalidOperationException("Cannot change document within another document change.");
        }

        updateCount++;
        
        if (updateCount == 1)
        {
            undoStack.StartUndoGroup();
            
            UpdateStarted?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Ends a group of document changes.
    /// </summary>
    public void EndUpdate()
    {
        VerifyAccess();
        
        if (isDocumentChanging)
        {
            throw new InvalidOperationException("Cannot end update within document change.");
        }

        if (updateCount == 0)
        {
            throw new InvalidOperationException("No update is active.");
        }

        if (updateCount == 1)
        {
            // fire change events **inside** the change group: event handlers can add
            // document changes to the change group
            FireChangeEvents();
            
            undoStack.EndUndoGroup();
            
            updateCount = 0;
            
            UpdateFinished?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            updateCount--;
        }
    }

    void IDocument.StartUndoableAction() => BeginUpdate();

    void IDocument.EndUndoableAction() => EndUpdate();

    IDisposable IDocument.OpenUndoGroup() => RunUpdate();
    #endregion

    #region alterations
    /// <summary>
    /// Inserts text.
    /// </summary>
    /// <param name="offset">The offset at which the text is inserted.</param>
    /// <param name="text">The new text.</param>
    /// <remarks>
    /// Anchors positioned exactly at the insertion offset will move according
    /// to their movement type--behind the inserted text by default.
    /// </remarks>
    public void Insert(int offset, string text) => Replace(offset, 0, new StringTextSource(text));

    /// <summary>
    /// Inserts text.
    /// </summary>
    /// <param name="offset">The offset at which the text is inserted.</param>
    /// <param name="text">The new text.</param>
    public void Insert(int offset, ITextSource text) => Replace(offset, 0, text);

    /// <summary>
    /// Inserts text.
    /// </summary>
    /// <param name="offset">The offset at which the text is inserted.</param>
    /// <param name="text">The new text.</param>
    /// <param name="movementType">
    /// Anchors positioned exactly at the insertion offset will move according to the anchor's movement type.
    /// For AnchorMovementType.Default, they will move according to the movement type specified by this parameter.
    /// The caret will also move according to the <paramref name="movementType"/> parameter.
    /// </param>
    public void Insert(int offset, string text, AnchorMovementType movementType)
    {
        if (movementType == AnchorMovementType.BeforeInsertion)
        {
            Replace(offset, 0, new StringTextSource(text), ChangeOffsetType.KeepAnchorsInFront);
        }
        else
        {
            Replace(offset, 0, new StringTextSource(text));
        }
    }

    /// <summary>
    /// Inserts text.
    /// </summary>
    /// <param name="offset">The offset at which the text is inserted.</param>
    /// <param name="text">The new text.</param>
    /// <param name="movementType">
    /// Anchors positioned exactly at the insertion offset will move according to the anchor's movement type.
    /// For AnchorMovementType.Default, they will move according to the movement type specified by this parameter.
    /// The caret will also move according to the <paramref name="movementType"/> parameter.
    /// </param>
    public void Insert(int offset, ITextSource text, AnchorMovementType movementType)
    {
        if (movementType == AnchorMovementType.BeforeInsertion)
        {
            Replace(offset, 0, text, ChangeOffsetType.KeepAnchorsInFront);
        }
        else
        {
            Replace(offset, 0, text);
        }
    }

    /// <summary>
    /// Removes text.
    /// </summary>
    public void Remove(ISegment segment) => Replace(segment, string.Empty);

    /// <summary>
    /// Removes text.
    /// </summary>
    /// <param name="offset">Starting offset of the text to be removed.</param>
    /// <param name="length">Length of the text to be removed.</param>
    public void Remove(int offset, int length) => Replace(offset, length, StringTextSource.Empty);

    internal bool isDocumentChanging;

    /// <summary>
    /// Replace text within the document.
    /// </summary>
    public void Replace(ISegment segment, string text)
    {
        if (segment == null)
        {
            throw new ArgumentNullException(nameof(segment));
        }

        Replace(segment.Offset, segment.Length, new StringTextSource(text));
    }

    /// <summary>
    /// Replace text within the document.
    /// </summary>
    public void Replace(ISegment segment, ITextSource text)
    {
        if (segment == null)
        {
            throw new ArgumentNullException(nameof(segment));
        }

        Replace(segment.Offset, segment.Length, text);
    }

    /// <summary>
    /// Replace text within the document.
    /// </summary>
    /// <param name="offset">The starting offset of the text to be replaced.</param>
    /// <param name="length">The length of the text to be replaced.</param>
    /// <param name="text">The new text.</param>
    public void Replace(int offset, int length, string text) => Replace(offset, length, new StringTextSource(text));

    /// <summary>
    /// Replace text within the document.
    /// </summary>
    /// <param name="offset">The starting offset of the text to be replaced.</param>
    /// <param name="length">The length of the text to be replaced.</param>
    /// <param name="text">The new text.</param>
    public void Replace(int offset, int length, ITextSource text) => Replace(offset, length, text);

    /// <summary>
    /// Replace text within the document.
    /// </summary>
    /// <param name="offset">The starting offset of the text to be replaced.</param>
    /// <param name="length">The length of the text to be replaced.</param>
    /// <param name="text">The new text.</param>
    /// <param name="changeOffsetType">The offsetChangeMappingType determines how offsets inside the old text are mapped to the new text.
    /// This affects how the anchors and segments inside the replaced region behave.</param>
    public void Replace(int offset, int length, string text, ChangeOffsetType changeOffsetType) => Replace(offset, length, new StringTextSource(text), changeOffsetType);

    /// <summary>
    /// Replaces text.
    /// </summary>
    /// <param name="offset">The starting offset of the text to be replaced.</param>
    /// <param name="length">The length of the text to be replaced.</param>
    /// <param name="text">The new text.</param>
    /// <param name="changeOffsetType">Determines how offsets inside the old text are mapped to the new text.
    /// This affects how the anchors and segments inside the replaced region behave.</param>
    /// <remarks><see cref="ChangeOffsetType.ReplaceCharacters"/> to see why the last character
    /// must be replaced.</remarks>
    public void Replace(int offset, int length, ITextSource text, ChangeOffsetType changeOffsetType)
    {
        if (text is null)
        {
            throw new ArgumentNullException(nameof(text));
        }

        switch (changeOffsetType)
        {
            case ChangeOffsetType.Default:

                Replace(offset, length, text);
                break;
            
            case ChangeOffsetType.KeepAnchorsInFront:

                Replace(offset, length, text, new ChangeOffsetCollection(
                    new ChangeOffset(offset, length, text.TextLength, false, true)));
                break;
            
            case ChangeOffsetType.RemoveThenInsert:

                if (length == 0 || text.TextLength == 0)
                {
                    // insertion or removal only; movement type doesn't matter
                    Replace(offset, length, text);
                }
                else
                {
                    ChangeOffsetCollection changes = new(2)
                    {
                        new ChangeOffset(offset, length, 0),
                        new ChangeOffset(offset, 0, text.TextLength)
                    };

                    changes.Freeze();
                    
                    Replace(offset, length, text, changes);
                }

                break;
            
            case ChangeOffsetType.ReplaceCharacters:
                
                if (length == 0 || text.TextLength == 0)
                {
                    Replace(offset, length, text);
                }
                else if (text.TextLength > length)
                {
                    var change = new ChangeOffset(offset + length - 1, 1, 1 + text.TextLength - length);
                    
                    Replace(offset, length, text, new ChangeOffsetCollection(change));
                }
                else if (text.TextLength < length)
                {
                    var change = new ChangeOffset(offset + text.TextLength, length - text.TextLength, 0, true, false);
                    
                    Replace(offset, length, text, new ChangeOffsetCollection(change));
                }
                else
                {
                    Replace(offset, length, text, ChangeOffsetCollection.Empty);
                }

                break;
            
            default:
                throw new ArgumentOutOfRangeException(nameof(changeOffsetType), $"Invalid enum {changeOffsetType} value.");
        }
    }

    /// <summary>
    /// Replaces text within the document.
    /// </summary>
    /// <param name="offset">The starting offset of the text to be replaced.</param>
    /// <param name="length">The length of the text to be replaced.</param>
    /// <param name="text">The new text.</param>
    /// <param name="changeOffsetColl">How to map old text offsets to the new text.
    /// This affects the behavior of anchors and segments inside the replaced region.
    /// <para>Offsets are changed with default movement when null, and with character
    /// replace when Empty.</para>
    /// <para>Any other value automatically freezes the collection to ensure thread-safety
    /// of the resulting DocumentChangeEventArgs instance.</para>
    /// <para>The collection must always be a valid explanation for the
    /// document change--see <see cref="ChangeOffsetCollection.IsValidForDocumentChange"/></para>
    /// </param>
    public void Replace(int offset, int length, string text, ChangeOffsetCollection changeOffsetColl) => Replace(offset, length, new StringTextSource(text), changeOffsetColl);

    /// <summary>
    /// Replaces text within the document.
    /// </summary>
    /// <param name="offset">The starting offset of the text to be replaced.</param>
    /// <param name="length">The length of the text to be replaced.</param>
    /// <param name="text">The new text.</param>
    /// <param name="changeOffsetColl">How to map old text offsets to the new text.</param>
    public void Replace(int offset, int length, ITextSource text, ChangeOffsetCollection changeOffsetColl = null!)
    {
        if (text == null)
        {
            throw new ArgumentNullException(nameof(text));
        }

        text = text.CreateSnapshot();

        changeOffsetColl?.Freeze();

        // Ensure that all changes take place inside an update group.
        // Will also take care of throwing an exception if inDocumentChanging is set.
        BeginUpdate();

        try
        {
            // protect document change against corruption by other changes inside the event handlers
            isDocumentChanging = true;

            try
            {
                // verify range after BeginUpdate() as the document could be modified inside UpdateStarted.
                if (offset < 0 || charTree.Length < offset)
                {
                    throw new ArgumentOutOfRangeException(nameof(offset), $"{offset} < 0 or {charTree.Length} < {offset}");
                }

                if (length < 0 || charTree.Length < offset + length)
                {
                    throw new ArgumentOutOfRangeException(nameof(length), $"{length} < 0 or {charTree.Length} < {offset} + {length}");
                }

                DoReplace(offset, length, text, changeOffsetColl!);
            }
            finally
            {
                isDocumentChanging = false;
            }
        }
        finally
        {
            EndUpdate();
        }
    }

    private void DoReplace(int offset, int length, ITextSource newText, ChangeOffsetCollection changeOffsetColl)
    {
        if (length == 0 && newText.TextLength == 0)
        {
            return;
        }

        // character replace mode doesn't touch the anchor tree so
        // is more performant than normal mode to replace a single character
        if (length == 1 && newText.TextLength == 1 && changeOffsetColl == null)
        {
            changeOffsetColl = ChangeOffsetCollection.Empty;
        }

        ITextSource removedText;

        if (length == 0)
        {
            removedText = StringTextSource.Empty;
        }
        else if (length < 100)
        {
            removedText = new StringTextSource(charTree.ToString(offset, length));
        }
        else
        {
            // use a rope if the removed string is long
            removedText = new TextSource(charTree.Slice(offset, length));
        }

        DocumentChangeEventArgs args = new(offset, removedText, newText, changeOffsetColl);

        // fire changing events event
        Changing?.Invoke(this, args);

        textChanging?.Invoke(this, args);

        undoStack.Push(this, args);

        cachedText = null;
        fireTextChanged = true;
        
        var eventQueue = new EventQueue();

        lock (_lock)
        {
            checkpointProvider.Append(args);

            // update the textBuffer and lineTree
            if (offset == 0 && length == charTree.Length)
            {
                // optimize replacing the whole document
                charTree.Clear();

                if (newText is TextSource textSource)
                {
                    charTree.InsertAt(0, textSource.GetTree());
                }
                else
                {
                    charTree.InsertText(0, newText.Text);
                }

                lineManager.Rebuild();
            }
            else
            {
                charTree.RemoveAt(offset, length);
                lineManager.Remove(offset, length);
#if DEBUG
                lineTree.ValidateData();
#endif
                if (newText is TextSource textSource)
                {
                    charTree.InsertAt(offset, textSource.GetTree());
                }
                else
                {
                    charTree.InsertText(offset, newText.Text);
                }

                lineManager.Insert(offset, newText);
#if DEBUG
                lineTree.ValidateData();
#endif
            }
        }

        // update text anchors
        if (changeOffsetColl is null)
        {
            anchorTree.HandleTextChange(args.CreateChange(), eventQueue);
        }
        else
        {
            foreach (var change in changeOffsetColl)
            {
                anchorTree.HandleTextChange(change, eventQueue);
            }
        }

        lineManager.ChangeComplete(args);

        // raise delayed events after our data structures are consistent again
        eventQueue.Flush();

        // fire DocumentChanged event
        Changed?.Invoke(this, args);

        textChanged?.Invoke(this, args);
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

    ILine IDocument.GetLineByNumber(int lineNumber) => GetLineByNumber(lineNumber);

    /// <summary>
    /// Gets a document lines by offset.
    /// Runtime: O(log n)
    /// </summary>
    public Line GetLineByOffset(int offset)
    {
        VerifyAccess();
        if (offset < 0 || offset > charTree.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(offset), offset, "0 <= offset <= " + charTree.Length.ToString());
        }
        return lineTree.LineBy(offset);
    }

    ILine IDocument.GetLineByOffset(int offset) => GetLineByOffset(offset);
    #endregion

    #region GetOffset / GetLocation
    /// <summary>
    /// Gets the offset from a text location.
    /// </summary>
    /// <seealso cref="GetLocation"/>
    public int GetOffset(Location location) => GetOffset(location.Line, location.Column);

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

        if (offset < 0 || charTree.Length < offset)
        {
            throw new ArgumentOutOfRangeException(nameof(offset), offset, "0 <= offset <= " + charTree.Length.ToString(CultureInfo.InvariantCulture));
        }
        return anchorTree.CreateAnchor(offset);
    }

    ITextAnchor IDocument.CreateAnchor(int offset) => CreateAnchor(offset);
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
    /// <summary>
    /// Debug-only version, speeds up frequent callers like <see cref="NewLineFinder"/> in release builds.
    /// </summary>
    [Conditional("DEBUG")]
    internal void DebugVerifyAccess() => VerifyAccess();

    /// <summary>
    /// Gets the document lines tree in string form.
    /// </summary>
    internal string GetLineTreeAsString() =>
#if DEBUG
        lineTree.GetTreeAsString();
#else
			return "Not available in release build.";
#endif


    /// <summary>
    /// Gets the text anchor tree in string form.
    /// </summary>
    internal string GetTextAnchorTreeAsString() =>
#if DEBUG
        anchorTree.GetTreeAsString();
#else
			return "Not available in release build.";
#endif

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

    object IServiceProvider.GetService(Type serviceType) => ServiceProvider.GetService(serviceType)!;
    #endregion

    #region FileName
    private string fileName;

    /// <inheritdoc/>
    public event EventHandler FileNameChanged;

    private void OnFileNameChanged(EventArgs e) => FileNameChanged?.Invoke(this, e);

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
