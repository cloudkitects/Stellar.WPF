namespace Stellar.WPF.Document;

/// <summary>
/// An undoable/redoable operation.
/// </summary>
public interface IUndoable
{
	/// <summary>
	/// Undo the last operation
	/// </summary>
	void Undo();

	/// <summary>
	/// Redo the last operation
	/// </summary>
	void Redo();
}

/// <summary>
/// An undoable/redoable operation with a reference to the history of operations.
/// </summary>
interface IUndoableWithContext : IUndoable
{
	void Undo(UndoStack stack);
	void Redo(UndoStack stack);
}
