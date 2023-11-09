using System;
using System.Diagnostics;
using System.Windows.Media.TextFormatting;

using Stellar.WPF.Utilities;

namespace Stellar.WPF.Rendering;

/// <summary>
/// WPF TextSource implementation that creates TextRuns for a VisualLine.
/// </summary>
sealed class VisualLineTextSource : TextSource, ITextRunContext
{
    string? cachedString;
    int cachedStringOffset;
    
    public VisualLine VisualLine { get; private set; }
    public TextView TextView { get; set; }
    public Document.Document Document { get; set; }
    public TextRunProperties GlobalTextRunProperties { get; set; }

    public VisualLineTextSource(VisualLine visualLine)
    {
        VisualLine = visualLine;
    }

    public override TextRun GetTextRun(int textSourceCharacterIndex)
    {
        try
        {
            foreach (var element in VisualLine.Elements)
            {
                if (textSourceCharacterIndex >= element.VisualColumn &&
                    textSourceCharacterIndex < element.VisualColumn + element.VisualLength)
                {
                    var relativeOffset = textSourceCharacterIndex - element.VisualColumn;

                    // caching as a lot can go wrong
                    var elementType = element.GetType().Name;

                    var run = element.CreateTextRun(textSourceCharacterIndex, this) ?? throw new ArgumentNullException($"{elementType}.CreateTextRun()");
                    
                    if (run.Length == 0)
                    {
                        throw new ArgumentException($"${elementType} run length = 0");
                    }

                    if (relativeOffset + run.Length > element.VisualLength)
                    {
                        throw new ArgumentException($"${elementType} run length = {run.Length} + relative offset = {relativeOffset} > {element.VisualLength}");
                    }

                    if (run is InlineObjectRun inlineRun)
                    {
                        inlineRun.VisualLine = VisualLine;
                        
                        VisualLine.hasInlineObjects = true;
                        
                        TextView.AddInlineObject(inlineRun);
                    }

                    return run;
                }
            }
            
            if (TextView.Options.ShowEndOfLine && textSourceCharacterIndex == VisualLine.VisualLength)
            {
                return CreateTextRunForNewLine();
            }

            return new TextEndOfParagraph(1);
        }
        catch (Exception exception)
        {
            Debug.WriteLine(exception.ToString());
            
            throw;
        }
    }

    TextRun CreateTextRunForNewLine()
    {
        var newlineText = "";
        var lastLine = VisualLine.LastLine;

        if (lastLine.SeparatorLength == 2)
        {
            newlineText = "¶";
        }
        else if (lastLine.SeparatorLength == 1)
        {
            var newlineChar = Document.GetCharAt(lastLine.Offset + lastLine.Length);

            if (newlineChar == '\r')
            {
                newlineText = "\\r";
            }
            else if (newlineChar == '\n')
            {
                newlineText = "\\n";
            }
            else
            {
                newlineText = "?";
            }
        }

        return new FormattedTextRun(new FormattedTextElement(TextView.nonPrintablesCache.GetText(newlineText, this)!, 0), GlobalTextRunProperties);
    }

    public override TextSpan<CultureSpecificCharacterBufferRange> GetPrecedingText(int textSourceCharacterIndexLimit)
    {
        try
        {
            foreach (var element in VisualLine.Elements)
            {
                if (textSourceCharacterIndexLimit > element.VisualColumn &&
                    textSourceCharacterIndexLimit <= element.VisualColumn + element.VisualLength)
                {
                    var span = element.GetPrecedingText(textSourceCharacterIndexLimit, this);

                    if (span is null)
                    {
                        break;
                    }

                    var relativeOffset = textSourceCharacterIndexLimit - element.VisualColumn;

                    if (span.Length > relativeOffset)
                    {
                        throw new ArgumentException($"{element.GetType().Name} span.Length = {span.Length} > {relativeOffset}");
                    }

                    return span;
                }
            }

            var empty = CharacterBufferRange.Empty;

            return new TextSpan<CultureSpecificCharacterBufferRange>(empty.Length, new CultureSpecificCharacterBufferRange(null, empty));
        }
        catch (Exception exception)
        {
            Debug.WriteLine(exception.ToString());

            throw;
        }
    }

    public override int GetTextEffectCharacterIndexFromTextSourceCharacterIndex(int textSourceCharacterIndex)
    {
        throw new NotSupportedException();
    }

    public StringSegment GetText(int offset, int length)
    {
        if (cachedString is not null)
        {
            if (offset >= cachedStringOffset && offset + length <= cachedStringOffset + cachedString.Length)
            {
                return new StringSegment(cachedString, offset - cachedStringOffset, length);
            }
        }

        cachedStringOffset = offset;
        
        return new StringSegment(cachedString = Document.GetText(offset, length));
    }
}