using System;
using System.Runtime.Serialization;

namespace Stellar.WPF.Editing;

/// <summary>
/// Wraps drag'n'drop exceptions that might get swallowed or 
/// incorrectly reported by WPF/COM to re-throw them later to be
/// caught by an app's unhandled exception handler.
/// </summary>
[Serializable()]
public class DragDropException : Exception
{
	/// <summary>
	/// Creates a new DragDropException.
	/// </summary>
	public DragDropException() : base()
	{
	}

	/// <summary>
	/// Creates a new DragDropException.
	/// </summary>
	public DragDropException(string message) : base(message)
	{
	}

	/// <summary>
	/// Creates a new DragDropException.
	/// </summary>
	public DragDropException(string message, Exception innerException) : base(message, innerException)
	{
	}

	/// <summary>
	/// Deserializes a DragDropException.
	/// </summary>
	protected DragDropException(SerializationInfo info, StreamingContext context) : base(info, context)
	{
	}
}
