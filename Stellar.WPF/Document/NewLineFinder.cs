namespace Stellar.WPF.Document;

internal static class NewLineFinder
{
    private static readonly char[] newline = { '\r', '\n' };

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
}