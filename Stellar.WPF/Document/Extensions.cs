using System;
using System.Globalization;
using System.Windows.Documents;

namespace Stellar.WPF.Document;

/// <summary>
/// extension methods for Document classes.
/// </summary>
public static class Extensions
{
    #region ISegment
    /// <summary>
    /// Gets whether <paramref name="segment"/> fully contains the specified segment.
    /// </summary>
    /// <remarks>
    /// Use <c>segment.Contains(offset, 0)</c> to detect whether a segment (end inclusive) contains offset;
    /// use <c>segment.Contains(offset, 1)</c> to detect whether a segment (end exclusive) contains offset.
    /// </remarks>
    public static bool Contains(this ISegment segment, int offset, int length) => segment.Offset <= offset && offset + length <= segment.EndOffset;

    /// <summary>
    /// Gets whether <paramref name="thisSegment"/> fully contains the specified segment.
    /// </summary>
    public static bool Contains(this ISegment thisSegment, ISegment segment) => segment != null && thisSegment.Offset <= segment.Offset && segment.EndOffset <= thisSegment.EndOffset;
    #endregion

    #region control characters
    /// <summary>
    /// The names of the Unicode C0 block: the first 32 ASCII characters.
    /// </summary>
    private static readonly string[] C0 = {
            "NUL", "SOH", "STX", "ETX", "EOT", "ENQ", "ACK", "BEL", "BS", "HT",
            "LF", "VT", "FF", "CR", "SO", "SI", "DLE", "DC1", "DC2", "DC3",
            "DC4", "NAK", "SYN", "ETB", "CAN", "EM", "SUB", "ESC", "FS", "GS",
            "RS", "US"
        };

    /// <summary>
    /// DEL (ASCII 127) plus the Unicode C1 block (Unicode 128 to 159).
    /// </summary>
    private static readonly string[] C1 = {
            "DEL",
            "PAD", "HOP", "BPH", "NBH", "IND", "NEL", "SSA", "ESA", "HTS", "HTJ",
            "VTS", "PLD", "PLU", "RI", "SS2", "SS3", "DCS", "PU1", "PU2", "STS",
            "CCH", "MW", "SPA", "EPA", "SOS", "SGCI", "SCI", "CSI", "ST", "OSC",
            "PM", "APC"
        };

    /// <summary>
    /// Gets the name of the control character, or the 4-digit hexadecimal value
    /// of the Unicode codepoint for unknown characters.
    /// </summary>
    public static string GetName(this char controlCharacter)
    {
        var num = (int)controlCharacter;

        if (num < C0.Length)
        {
            return C0[num];
        }

        if (num >= 127 && num <= 159)
        {
            return C1[num - 127];
        }

        return num.ToString("x4", CultureInfo.InvariantCulture);
    }
    #endregion

    #region caret
    /// <summary>
    /// Gets the next caret position.
    /// </summary>
    /// <param name="textSource">The text source.</param>
    /// <param name="offset">The start offset inside the text source.</param>
    /// <param name="direction">The search direction (forwards or backwards).</param>
    /// <param name="mode">The mode for caret positioning.</param>
    /// <returns>The offset of the next caret position, or -1 if there is no further caret position
    /// in the text source.</returns>
    /// <remarks>
    /// This method is NOT equivalent to the actual caret movement when using VisualLine.GetNextCaretPosition.
    /// In real caret movement, there are additional caret stops at line starts and ends. This method
    /// treats linefeeds as simple whitespace.
    /// </remarks>
    public static int GetNextCaretPosition(this ITextSource textSource, int offset, LogicalDirection direction, CaretPositioningMode mode)
    {
        switch (mode)
        {
            case CaretPositioningMode.Normal:
            case CaretPositioningMode.EveryCodepoint:
            case CaretPositioningMode.WordBorder:
            case CaretPositioningMode.WordBorderOrSymbol:
            case CaretPositioningMode.WordStart:
            case CaretPositioningMode.WordStartOrSymbol:
                break;
            default:
                throw new ArgumentException($"Unsupported {nameof(CaretPositioningMode)}: {mode}");
        }
        
        if (direction != LogicalDirection.Backward && direction != LogicalDirection.Forward)
        {
            throw new ArgumentException($"Unsupported {nameof(LogicalDirection)}: {direction}");
        }
        
        var textLength = textSource.TextLength;
        
        if (textLength <= 0)
        {
            // empty document? has a normal caret position at 0, though no word borders
            if (IsNormal(mode))
            {
                if ((offset > 0 && direction == LogicalDirection.Backward) ||
                    (offset < 0 && direction == LogicalDirection.Forward))
                {
                    return 0;
                }
            }

            return -1;
        }

        while (true)
        {
            var nextPos = offset + (direction == LogicalDirection.Backward
                ? -1
                : +1);

            // there's no further caret position in the text source, or
            // it's outside the valid range
            if (nextPos < 0 || textLength < nextPos)
            {
                return -1;
            }

            // check if we've run against the textSource borders.
            // a 'textSource' usually isn't the whole document, but a single VisualLineElement.
            if (nextPos == 0)
            {
                // at the document start, there's only a word border
                // if the first character is not whitespace
                if (IsNormal(mode) || !char.IsWhiteSpace(textSource.GetCharAt(0)))
                {
                    return nextPos;
                }
            }
            else if (nextPos == textLength)
            {
                // at the document end, there's never a word start
                if (mode != CaretPositioningMode.WordStart && mode != CaretPositioningMode.WordStartOrSymbol)
                {
                    // at the document end, there's only a word border
                    // if the last character is not whitespace
                    if (IsNormal(mode) || !char.IsWhiteSpace(textSource.GetCharAt(textLength - 1)))
                    {
                        return nextPos;
                    }
                }
            }
            else
            {
                var charBefore = textSource.GetCharAt(nextPos - 1);
                var charAfter = textSource.GetCharAt(nextPos);

                // Don't stop in the middle of a surrogate pair
                if (!char.IsSurrogatePair(charBefore, charAfter))
                {
                    var classBefore = GetCharacterClass(charBefore);
                    var classAfter = GetCharacterClass(charAfter);

                    // get correct class for characters outside BMP:
                    if (char.IsLowSurrogate(charBefore) && nextPos >= 2)
                    {
                        classBefore = GetCharacterClass(textSource.GetCharAt(nextPos - 2), charBefore);
                    }
                    
                    if (char.IsHighSurrogate(charAfter) && nextPos + 1 < textLength)
                    {
                        classAfter = GetCharacterClass(charAfter, textSource.GetCharAt(nextPos + 1));
                    }
                    
                    if (StopBetweenCharacters(mode, classBefore, classAfter))
                    {
                        return nextPos;
                    }
                }
            }

            offset = nextPos;
        }
    }

    static bool IsNormal(this CaretPositioningMode mode)
    {
        return mode == CaretPositioningMode.Normal || mode == CaretPositioningMode.EveryCodepoint;
    }

    static bool StopBetweenCharacters(this CaretPositioningMode mode, CharacterClass charBefore, CharacterClass charAfter)
    {
        if (mode == CaretPositioningMode.EveryCodepoint)
        {
            return true;
        }

        // Don't stop in the middle of a grapheme
        if (charAfter == CharacterClass.CombiningMark)
        {
            return false;
        }

        // Stop after every grapheme in normal mode
        if (mode == CaretPositioningMode.Normal)
        {
            return true;
        }

        if (charBefore == charAfter)
        {
            if (charBefore == CharacterClass.Other &&
                (mode == CaretPositioningMode.WordBorderOrSymbol || mode == CaretPositioningMode.WordStartOrSymbol))
            {
                // With the "OrSymbol" modes, there's a word border and start between any two unknown characters
                return true;
            }
        }
        else
        {
            // this looks like a border;
            // if we're looking for word starts, check that this is a word start (and not a word end)
            // if we're just checking for word borders, accept unconditionally
            if (!((mode == CaretPositioningMode.WordStart || mode == CaretPositioningMode.WordStartOrSymbol)
                  && (charAfter == CharacterClass.Whitespace || charAfter == CharacterClass.LineTerminator)))
            {
                return true;
            }
        }
        
        return false;
    }

    /// <summary>
    /// Gets whether the character is whitespace, part of an identifier,
    /// or a line terminator.
    /// </summary>
    public static CharacterClass GetCharacterClass(this char c)
    {
        if (c == '\r' || c == '\n')
        {
            return CharacterClass.LineTerminator;
        }

        if (c == '_')
        {
            return CharacterClass.IdentifierPart;
        }

        return GetCharacterClass(char.GetUnicodeCategory(c));
    }

    /// <summary>
    /// Get the character class out of a Unicode surrogate pair.
    /// </summary>
    static CharacterClass GetCharacterClass(char hi, char lo)
    {
        if (char.IsSurrogatePair(hi, lo))
        {
            return GetCharacterClass(char.GetUnicodeCategory($"{hi}{lo}", 0));
        }
        else
        {
            // malformed surrogate pair
            return CharacterClass.Other;
        }
    }

    static CharacterClass GetCharacterClass(this UnicodeCategory c)
    {
        return c switch
        {
            UnicodeCategory.SpaceSeparator or
            UnicodeCategory.LineSeparator or
            UnicodeCategory.ParagraphSeparator or
            UnicodeCategory.Control => CharacterClass.Whitespace,
            
            UnicodeCategory.UppercaseLetter or
            UnicodeCategory.LowercaseLetter or
            UnicodeCategory.TitlecaseLetter or
            UnicodeCategory.ModifierLetter or
            UnicodeCategory.OtherLetter or
            UnicodeCategory.DecimalDigitNumber => CharacterClass.IdentifierPart,
            
            UnicodeCategory.NonSpacingMark or
            UnicodeCategory.SpacingCombiningMark or
            UnicodeCategory.EnclosingMark => CharacterClass.CombiningMark,
            
            _ => CharacterClass.Other,
        };
    }
    #endregion

    #region ITextSource
    /// <summary>
    /// Gets a single indentation segment starting at <paramref name="offset"/> - at most one tab
    /// or <paramref name="indentationSize"/> spaces.
    /// </summary>
    /// <param name="textSource">The text source.</param>
    /// <param name="offset">The offset where the indentation segment starts.</param>
    /// <param name="indentationSize">The size of an indentation unit. See <see cref="TextEditorOptions.IndentationSize"/>.</param>
    /// <returns>The indentation segment.
    /// If there is no indentation character at the specified <paramref name="offset"/>,
    /// an empty segment is returned.</returns>
    public static ISegment GetSingleIndentationSegment(this ITextSource textSource, int offset, int indentationSize)
    {
        if (textSource is null)
        {
            throw new ArgumentNullException(nameof(textSource));
        }

        var pos = offset;

        while (pos < textSource.TextLength)
        {
            var c = textSource.GetCharAt(pos);

            if (c == '\t')
            {
                if (pos == offset)
                {
                    return new SimpleSegment(offset, 1);
                }
                
                break;
            }
            else if (c == ' ')
            {
                if (pos - offset >= indentationSize)
                {
                    break;
                }
            }
            else
            {
                break;
            }

            pos++;
        }

        return new SimpleSegment(offset, pos - offset);
    }

    /// <summary>
    /// Gets all whitespace (' ' and '\t', but no newlines) after offset.
    /// </summary>
    /// <param name="textSource">The text source.</param>
    /// <param name="offset">The offset where the whitespace starts.</param>
    /// <returns>The segment containing the whitespace.</returns>
    public static ISegment GetWhitespaceAfter(this ITextSource textSource, int offset)
    {
        if (textSource is null)
        {
            throw new ArgumentNullException(nameof(textSource));
        }

        int pos;

        for (pos = offset; pos < textSource.TextLength; pos++)
        {
            var c = textSource.GetCharAt(pos);

            if (c != ' ' && c != '\t')
            {
                break;
            }
        }

        return new SimpleSegment(offset, pos - offset);
    }

    /// <summary>
    /// Gets all whitespace (' ' and '\t', but no newlines) before offset.
    /// </summary>
    /// <param name="textSource">The text source.</param>
    /// <param name="offset">The offset where the whitespace ends.</param>
    /// <returns>The segment containing the whitespace.</returns>
    public static ISegment GetWhitespaceBefore(this ITextSource textSource, int offset)
    {
        if (textSource is null)
        {
            throw new ArgumentNullException(nameof(textSource));
        }

        int pos;

        for (pos = offset - 1; pos >= 0; pos--)
        {
            var c = textSource.GetCharAt(pos);
            
            if (c != ' ' && c != '\t')
            {
                break;
            }
        }

        pos++; // go back the one character that isn't whitespace
        
        return new SimpleSegment(pos, offset - pos);
    }

    /// <summary>
    /// Gets the leading whitespace segment on the document line.
    /// </summary>
    public static ISegment GetLeadingWhitespace(this Document document, Line line)
    {
        if (line is null)
        {
            throw new ArgumentNullException(nameof(line));
        }

        return GetWhitespaceAfter(document, line.Offset);
    }

    /// <summary>
    /// Gets the trailing whitespace segment on the document line.
    /// </summary>
    public static ISegment GetTrailingWhitespace(this Document document, Line line)
    {
        if (line is null)
        {
            throw new ArgumentNullException(nameof(line));
        }

        var segment = GetWhitespaceBefore(document, line.EndOffset);

        // If the whole line consists of whitespace, we consider all of it as leading whitespace,
        // so return an empty segment as trailing whitespace.
        if (segment.Offset == line.Offset)
        {
            return new SimpleSegment(line.EndOffset, 0);
        }
        
        return segment;
    }
    #endregion
}
