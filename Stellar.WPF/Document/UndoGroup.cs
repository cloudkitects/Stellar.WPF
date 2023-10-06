using System;
using System.Diagnostics;

using Stellar.WPF.Utilities;

namespace Stellar.WPF.Document;

/// <summary>
/// A stack of the last n operations from the undo stack,
/// grouped into a single operation.
/// </summary>
internal sealed class UndoGroup : IUndoableWithContext
{
    private readonly IUndoable[] undos;

	public UndoGroup(Deque<IUndoable> stack, int count)
	{
		if (stack is null)
		{
			throw new ArgumentNullException(nameof(stack));
		}

		Debug.Assert(count > 0, $"UndoGroup: {count} <= 0");
		Debug.Assert(count <= stack.Count, $"UndoGroup: {count} <= 0");

		undos = new IUndoable[count];

		for (var i = 0; i < count; ++i)
		{
			undos[i] = stack.PopBack();
		}
	}

	public void Undo()
	{
		for (var i = 0; i < undos.Length; ++i)
		{
			undos[i].Undo();
		}
	}

	public void Undo(UndoStack stack)
	{
		for (var i = 0; i < undos.Length; ++i)
		{
			stack.RunUndo(undos[i]);
		}
	}

	public void Redo()
	{
		for (var i = undos.Length - 1; i >= 0; --i)
		{
			undos[i].Redo();
		}
	}

	public void Redo(UndoStack stack)
	{
		for (var i = undos.Length - 1; i >= 0; --i)
		{
			stack.RunRedo(undos[i]);
		}
	}
}
