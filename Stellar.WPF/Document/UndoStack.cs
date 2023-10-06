using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;

using Stellar.WPF.Utilities;

namespace Stellar.WPF.Document;

/// <summary>
/// Undo stack implementation.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1711:IdentifiersShouldNotHaveIncorrectSuffix")]
public sealed class UndoStack : INotifyPropertyChanged
{
    /// <summary>
    /// The state of the undo stack, either
    /// listening for changes, playing revert/repeat operations
    /// back or modifying the document while playing back.
    /// Used to check no one but the stack performs changes
    /// during undo events.
    /// </summary>
    internal enum State
	{
		Listen,
		Playback,
		Modify
	}
	
	internal State state = State.Listen;

    private readonly Deque<IUndoable> undostack = new Deque<IUndoable>();
    private readonly Deque<IUndoable> redostack = new Deque<IUndoable>();
    
	private int sizeLimit = int.MaxValue;
    private int undoGroupDepth;
    private int actionCountInUndoGroup;
    private int optionalActionCount;
    
	private object? lastGroupDescriptor;
    
	private bool allowContinue;

    #region IsOriginalFile implementation
    // implements feature request SD2-784 - File still considered dirty after undoing all changes

    /// <summary>
    /// Number of times undo must be executed until the original state is reached.
    /// Negative: number of times redo must be executed until the original state is reached.
    /// Special case: int.MinValue == original state is unreachable
    /// </summary>
    private int elementsOnUndoUntilOriginalFile;
    private bool isOriginalFile = true;

    /// <summary>
    /// Gets whether the document is currently in its original state (no modifications).
    /// </summary>
    public bool IsOriginalFile => isOriginalFile;

    private void RecalcIsOriginalFile()
	{
		var newIsOriginalFile = (elementsOnUndoUntilOriginalFile == 0);

		if (newIsOriginalFile != isOriginalFile)
		{
			isOriginalFile = newIsOriginalFile;
			
			NotifyPropertyChanged(nameof(IsOriginalFile));
		}
	}

	/// <summary>
	/// Marks the current state as original. Discards any previous "original" markers.
	/// </summary>
	public void MarkAsOriginalFile()
	{
		elementsOnUndoUntilOriginalFile = 0;
		
		RecalcIsOriginalFile();
	}

	/// <summary>
	/// Discards the current "original" marker.
	/// </summary>
	public void DiscardOriginalFileMarker()
	{
		elementsOnUndoUntilOriginalFile = int.MinValue;
		
		RecalcIsOriginalFile();
	}

    /// <summary>
	/// 
	/// </summary>
	/// <remarks>
	/// It doesn't recalc whether original as it must wait until
	/// the end of the undo group.
	/// </remarks>
	private void FileModified(int newElementsOnUndoStack)
	{
		if (elementsOnUndoUntilOriginalFile == int.MinValue)
		{
			return;
		}

		elementsOnUndoUntilOriginalFile += newElementsOnUndoStack;
		
		if (elementsOnUndoUntilOriginalFile > undostack.Count)
        {
            elementsOnUndoUntilOriginalFile = int.MinValue;
        }
    }
    #endregion

    /// <summary>
    /// Gets if the undo stack currently accepts changes.
    /// Is false while an undo action is running.
    /// </summary>
    public bool AcceptChanges => state == State.Listen;

    /// <summary>
    /// Gets if there are actions on the undo stack.
    /// Use the PropertyChanged event to listen to changes of this property.
    /// </summary>
    public bool CanUndo => undostack.Count > 0;

    /// <summary>
    /// Gets if there are actions on the redo stack.
    /// Use the PropertyChanged event to listen to changes of this property.
    /// </summary>
    public bool CanRedo => redostack.Count > 0;

    /// <summary>
    /// Gets/Sets the limit on the number of items on the undo stack.
    /// </summary>
    /// <remarks>The size limit is enforced only on the number of stored top-level undo groups.
    /// Elements within undo groups do not count towards the size limit.</remarks>
    public int SizeLimit
	{
		get { return sizeLimit; }
		
		set {
			if (value < 0)
			{
                throw new ArgumentOutOfRangeException(nameof(value), $"{value} < 0");
            }

            if (sizeLimit != value)
			{
				sizeLimit = value;
				
				NotifyPropertyChanged(nameof(SizeLimit));

				if (undoGroupDepth == 0)
				{
					EnforceSizeLimit();
				}
			}
		}
	}

    private void EnforceSizeLimit()
	{
		Debug.Assert(undoGroupDepth == 0);

		while (undostack.Count > sizeLimit)
		{
			undostack.PopFront();
		}

		while (redostack.Count > sizeLimit)
		{
			redostack.PopFront();
		}
	}

    /// <summary>
    /// The group descriptor of the current top-level undo group if open,
    /// or that of the previous group if none.
    /// </summary>
    /// <remarks>The group descriptor can be used to join adjacent undo groups:
    /// use one to mark changes, compare with the last on the second action and
	/// use <see cref="StartContinuedUndoGroup"/>  to join both.</remarks>
    public object? LastGroupDescriptor => lastGroupDescriptor;

    /// <summary>
    /// Starts grouping changes.
    /// Maintains a counter so that nested calls are possible.
    /// </summary>
    public void StartUndoGroup()
	{
		StartUndoGroup(null!);
	}

	/// <summary>
	/// Starts grouping changes.
	/// Maintains a counter so that nested calls are possible.
	/// </summary>
	/// <param name="groupDescriptor">An object that is stored with the undo group.
	/// If this is not a top-level undo group, the parameter is ignored.</param>
	public void StartUndoGroup(object groupDescriptor)
	{
		if (undoGroupDepth == 0)
		{
			actionCountInUndoGroup = 0;
			optionalActionCount = 0;
			lastGroupDescriptor = groupDescriptor;
		}

		undoGroupDepth++;
	}

	/// <summary>
	/// Starts grouping changes, continuing with the previously closed undo group if possible.
	/// Maintains a counter so that nested calls are possible.
	/// If the call to StartContinuedUndoGroup is a nested call, it behaves exactly
	/// as <see cref="StartUndoGroup()"/>, only top-level calls can continue existing undo groups.
	/// </summary>
	/// <param name="groupDescriptor">An object that is stored with the undo group.
	/// If this is not a top-level undo group, the parameter is ignored.</param>
	public void StartContinuedUndoGroup(object groupDescriptor = null!)
	{
		if (undoGroupDepth == 0)
		{
			actionCountInUndoGroup = (allowContinue && undostack.Count > 0) ? 1 : 0;
			optionalActionCount = 0;
			lastGroupDescriptor = groupDescriptor;
		}

		undoGroupDepth++;
	}

	/// <summary>
	/// Stops grouping changes.
	/// </summary>
	public void EndUndoGroup()
	{
		if (undoGroupDepth == 0)
		{
			throw new InvalidOperationException("There are no open undo groups");
		}
		
		undoGroupDepth--;

		if (undoGroupDepth == 0)
		{
			Debug.Assert(state == State.Listen || actionCountInUndoGroup == 0);
			
			allowContinue = true;
			
			if (actionCountInUndoGroup == optionalActionCount)
			{
				// only optional actions: don't store them
				for (var i = 0; i < optionalActionCount; i++)
				{
					undostack.PopBack();
				}

				allowContinue = false;
			}
			else if (actionCountInUndoGroup > 1)
			{
				// combine all actions within the group into a single grouped action
				undostack.PushBack(new UndoGroup(undostack, actionCountInUndoGroup));
				
				FileModified(-actionCountInUndoGroup + 1 + optionalActionCount);
			}

			EnforceSizeLimit();
			RecalcIsOriginalFile();
		}
	}

    /// <summary>
    /// Throws an InvalidOperationException if an undo group is current open.
    /// </summary>
    private void ThrowIfUndoGroupOpen()
	{
		if (undoGroupDepth != 0)
		{
			undoGroupDepth = 0;
			
			throw new InvalidOperationException("No undo group should be open at this point");
		}

		if (state != State.Listen)
		{
			throw new InvalidOperationException("This method cannot be called while an undo operation is being performed");
		}
	}

    private List<Document>? affectedDocuments;

	internal void RegisterAffectedDocument(Document document)
	{
		if (affectedDocuments == null)
        {
            affectedDocuments = new List<Document>();
        }

        if (!affectedDocuments.Contains(document)) {
			affectedDocuments.Add(document);
			document.BeginUpdate();
		}
	}

    private void CallEndUpdateOnAffectedDocuments()
	{
		if (affectedDocuments != null) {
			foreach (Document doc in affectedDocuments) {
				doc.EndUpdate();
			}
			affectedDocuments = null;
		}
	}

	/// <summary>
	/// Call this method to undo the last operation on the stack
	/// </summary>
	public void Undo()
	{
		ThrowIfUndoGroupOpen();

		if (undostack.Count > 0)
		{
			// disallow continuing undo groups after undo operation
			lastGroupDescriptor = null; allowContinue = false;
			// fetch operation to undo and move it to redo stack
			var uedit = undostack.PopBack();

			redostack.PushBack(uedit);
			
			state = State.Playback;
			
			try
			{
				RunUndo(uedit);
			}
			finally
			{
				state = State.Listen;
				FileModified(-1);
				CallEndUpdateOnAffectedDocuments();
			}
			
			RecalcIsOriginalFile();
			
			if (undostack.Count == 0)
            {
                NotifyPropertyChanged(nameof(CanUndo));
            }

            if (redostack.Count == 1)
            {
                NotifyPropertyChanged(nameof(CanRedo));
            }
        }
	}

	internal void RunUndo(IUndoable undoable)
	{
		var undoableWithContext = undoable as IUndoableWithContext;
		
		if (undoableWithContext is not null)
        {
            undoableWithContext.Undo(this);
        }
        else
        {
            undoable.Undo();
        }
    }

	/// <summary>
	/// Call this method to redo the last undone operation
	/// </summary>
	public void Redo()
	{
		ThrowIfUndoGroupOpen();

		if (redostack.Count > 0)
		{
			lastGroupDescriptor = null;
			allowContinue = false;
			
			var uedit = redostack.PopBack();
			
			undostack.PushBack(uedit);
			
			state = State.Playback;
			
			try
			{
				RunRedo(uedit);
			}
			finally
			{
				state = State.Listen;
				
				FileModified(1);
				
				CallEndUpdateOnAffectedDocuments();
			}
			
			RecalcIsOriginalFile();
			
			if (redostack.Count == 0)
            {
                NotifyPropertyChanged(nameof(CanRedo));
            }

            if (undostack.Count == 1)
            {
                NotifyPropertyChanged(nameof(CanUndo));
            }
        }
	}

	internal void RunRedo(IUndoable redoable)
	{
		var redoableWithContext = redoable as IUndoableWithContext;
		
		if (redoableWithContext is not null)
        {
            redoableWithContext.Redo(this);
        }
        else
        {
            redoable.Redo();
        }
    }

	/// <summary>
	/// Call this method to push an UndoableOperation on the undostack.
	/// The redostack will be cleared if you use this method.
	/// </summary>
	public void Push(IUndoable undoable)
	{
		Push(undoable, false);
	}

	/// <summary>
	/// Call this method to push an UndoableOperation on the undostack.
	/// However, the operation will be only stored if the undo group contains a
	/// non-optional operation.
	/// Use this method to store the caret position/selection on the undo stack to
	/// prevent having only actions that affect only the caret and not the document.
	/// </summary>
	public void PushOptional(IUndoable undoable)
	{
		if (undoGroupDepth == 0)
        {
            throw new InvalidOperationException("Cannot use PushOptional outside of undo group");
        }

        Push(undoable, true);
	}

    private void Push(IUndoable undoable, bool isOptional)
	{
		if (undoable is null)
		{
			throw new ArgumentNullException(nameof(undoable));
		}

		if (state == State.Listen && sizeLimit > 0)
		{
			bool wasEmpty = undostack.Count == 0;
			bool needsUndoGroup = undoGroupDepth == 0;

			if (needsUndoGroup)
            {
                StartUndoGroup();
            }

            undostack.PushBack(undoable);

			actionCountInUndoGroup++;

			if (isOptional)
            {
                optionalActionCount++;
            }
            else
            {
                FileModified(1);
            }

            if (needsUndoGroup)
            {
                EndUndoGroup();
            }

            if (wasEmpty)
            {
                NotifyPropertyChanged(nameof(CanUndo));
            }

            ClearRedoStack();
		}
	}

	/// <summary>
	/// Call this method, if you want to clear the redo stack
	/// </summary>
	public void ClearRedoStack()
	{
		if (redostack.Count != 0)
		{
			redostack.Clear();
			
			NotifyPropertyChanged(nameof(CanRedo));
			
			// if the "original file" marker is on the redo stack: remove it
			if (elementsOnUndoUntilOriginalFile < 0)
            {
                elementsOnUndoUntilOriginalFile = int.MinValue;
            }
        }
	}

	/// <summary>
	/// Clears both the undo and redo stack.
	/// </summary>
	public void ClearAll()
	{
		ThrowIfUndoGroupOpen();

		actionCountInUndoGroup = 0;
		optionalActionCount = 0;
		
		if (undostack.Count != 0)
		{
			lastGroupDescriptor = null;
			allowContinue = false;
			
			undostack.Clear();
			
			NotifyPropertyChanged(nameof(CanUndo));
		}

		ClearRedoStack();
	}

	internal void Push(Document document, DocumentChangeEventArgs e)
	{
		if (state == State.Playback)
		{
			throw new InvalidOperationException("No changes are allowed during undo/redo operations.");
		}

		if (state == State.Modify)
		{
			state = State.Playback; // allow only 1 change per expected modification
		}
		else
		{
			Push(new UndoableChange(document, e));
		}
	}

	/// <summary>
	/// Is raised when a property (CanUndo, CanRedo) changed.
	/// </summary>
	public event PropertyChangedEventHandler? PropertyChanged;

    private void NotifyPropertyChanged(string propertyName)
	{
		if (PropertyChanged is not null)
		{
			PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
		}
	}
}
