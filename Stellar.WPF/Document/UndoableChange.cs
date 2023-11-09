using System.Diagnostics;

namespace Stellar.WPF.Document;

/// <summary>
/// Describes an undoable change to a document.
/// </summary>
sealed class UndoableChange : IUndoableWithContext
{
    private readonly Document document;
    private readonly DocumentChangeEventArgs change;

	public UndoableChange(Document document, DocumentChangeEventArgs change)
	{
		this.document = document;
		this.change = change;
	}

	public void Undo(UndoStack stack)
	{
		Debug.Assert(stack.state == UndoStack.State.Playback);
		
		stack.RegisterAffectedDocument(document);
		
		stack.state = UndoStack.State.Modify;
		
		Undo();
		
		stack.state = UndoStack.State.Playback;
	}

	public void Redo(UndoStack stack)
	{
		Debug.Assert(stack.state == UndoStack.State.Playback);

		stack.RegisterAffectedDocument(document);
		
		stack.state = UndoStack.State.Modify;
		
		Redo();
		
		stack.state = UndoStack.State.Playback;
	}

	public void Undo()
	{
		var map = change.OffsetChangesOrNull;

		document.Replace(change.Offset, change.InsertionLength, change.RemovedText, map?.Invert()!);
	}

	public void Redo()
	{
		document.Replace(change.Offset, change.RemovalLength, change.InsertedText, change.OffsetChangesOrNull);
	}
}
