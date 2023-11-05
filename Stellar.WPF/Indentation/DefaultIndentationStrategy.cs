using System;

using Stellar.WPF.Document;

namespace Stellar.WPF.Indentation;

/// <summary>
/// Handles indentation by copying the indentation from the previous line.
/// Does not support indenting multiple lines.
/// </summary>
public class DefaultIndentationStrategy : IIndentationStrategy
{
	/// <inheritdoc/>
	public virtual void IndentLine(Document.Document document, Line line)
	{
		if (document is null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        if (line is null)
        {
            throw new ArgumentNullException(nameof(line));
        }

        var previousLine = line.PreviousLine;
		
		if (previousLine is not null)
		{
			var indentationSegment = document.GetWhitespaceAfter(previousLine.Offset);
			var indentation = document.GetText(indentationSegment);
			
			// copy indentation to line
			indentationSegment = document.GetWhitespaceAfter(line.Offset);

            // replace using a change type that guarantees the caret moves behind the new indentation
            document.Replace(indentationSegment.Offset, indentationSegment.Length, indentation,
							 ChangeOffsetType.RemoveThenInsert);
		}
	}

	/// <summary>
	/// Does nothing: indenting multiple lines is useless without a smart indentation strategy.
	/// </summary>
	public virtual void IndentLines(Document.Document document, int frinLine, int toLine)
	{
	}
}
