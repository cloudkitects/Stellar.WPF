using Stellar.WPF.Document;

namespace Stellar.WPF.Indentation;

/// <summary>
/// Strategy how the text editor handles indentation when new lines are inserted.
/// </summary>
public interface IIndentationStrategy
{
	/// <summary>
	/// Sets the indentation for the specified line.
	/// Usually this is constructed from the indentation of the previous line.
	/// </summary>
	void IndentLine(Document.Document document, Line line);

	/// <summary>
	/// Reindents a set of lines.
	/// </summary>
	void IndentLines(Document.Document document, int frLine, int toLine);
}
