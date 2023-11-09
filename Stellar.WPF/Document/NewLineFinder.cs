using System;
using System.Linq;
using System.Text;

namespace Stellar.WPF.Document;

internal static class NewLineFinder
{
    private static readonly char[] newline = { '\r', '\n' };

    internal static readonly string[] NewlineStrings = { "\r\n", "\r", "\n" };

    /// <summary>
    /// Get the location of the next new line character in a string, or SimpleSegment.Invalid if none is found.
    /// </summary>
    internal static SimpleSegment Next(string text, int offset)
    {
        var pos = text.IndexOfAny(newline, offset);

        if (pos >= 0)
        {
            if (text[pos] == '\r')
            {
                if (pos + 1 < text.Length && text[pos + 1] == '\n')
                {
                    return new SimpleSegment(pos, 2);
                }
            }

            return new SimpleSegment(pos, 1);
        }

        return SimpleSegment.Invalid;
    }

    /// <summary>
    /// Get the location of the next new line character in a text source, or SimpleSegment.Invalid if none is found.
    /// </summary>
    internal static SimpleSegment Next(ITextSource text, int offset)
    {
        var textLength = text.TextLength;
        var pos = text.IndexOfAny(newline, offset, textLength - offset);

        if (pos >= 0)
        {
            if (text.GetCharAt(pos) == '\r')
            {
                if (pos + 1 < textLength && text.GetCharAt(pos + 1) == '\n')
                {
                    return new SimpleSegment(pos, 2);
                }
            }

            return new SimpleSegment(pos, 1);
        }

        return SimpleSegment.Invalid;
    }

    /// <summary>
    /// Gets the new line string used in the document at the specified line.
    /// </summary>
    internal static string GetNewLineString(this IDocument document, int lineNumber)
    {
        var line = document.GetLineByNumber(lineNumber);
        
        if (line.SeparatorLength == 0)
        {
            // no separator at the end of the document; use the one from the previous line
            line = line.PreviousLine;

            if (line == null)
            {
                return Environment.NewLine;
            }
        }

        return document.GetText(line.Offset + line.Length, line.SeparatorLength);
    }

    /// <summary>
    /// Normalizes all new lines in <paramref name="input"/> to be <paramref name="newLine"/>.
    /// </summary>
    public static string? NormalizeNewLines(this string input, string newLine)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        if (!NewlineStrings.Contains(newLine))
        {
            throw new ArgumentException($"{nameof(newline)} must be a known new line sequence");
        }

        var segment = Next(input, 0);

        // the input does not contain any new lines
        if (segment == SimpleSegment.Invalid)
        {
            return input;
        }

        var b = new StringBuilder(input.Length);
        var lastEndOffset = 0;

        do
        {
            b.Append(input, lastEndOffset, segment.Offset - lastEndOffset);
            b.Append(newLine);
            
            lastEndOffset = segment.EndOffset;
            segment = Next(input, lastEndOffset);
        }
        while (segment != SimpleSegment.Invalid);
        
        // remaining string (after last newline)
        b.Append(input, lastEndOffset, input.Length - lastEndOffset);
        
        return b.ToString();
    }
}