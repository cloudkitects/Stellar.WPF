using System;
using System.Linq;

using Stellar.WPF.Document;

namespace Stellar.WPF.Rendering;

public abstract class DocumentRenderer : Renderer
{
    private Line? currentLine;
    private int firstLineStart;
    private int currentLineStart, currentLineEnd;

    /// <summary>
    /// Gets the current ITextRunContext.
    /// </summary>
    protected ITextRunContext? CurrentContext { get; private set; }

    /// <inheritdoc/>
    protected override void Render(ITextRunContext context)
    {
        CurrentContext = context ?? throw new ArgumentNullException(nameof(context));

        currentLine = context.VisualLine.FirstLine;
        
        firstLineStart = currentLineStart = currentLine.Offset;
        currentLineEnd = currentLineStart + currentLine.Length;
        
        var currentLineTotalEnd = currentLineStart + currentLine.Length;

        if (context.VisualLine.FirstLine == context.VisualLine.LastLine)
        {
            RenderLine(currentLine);
        }
        else
        {
            RenderLine(currentLine);

            // ColorizeLine modifies the visual line elements, loop through a copy of the line elements
            foreach (var e in context.VisualLine.Elements.ToArray())
            {
                var elementOffset = firstLineStart + e.RelativeTextOffset;
                
                if (elementOffset >= currentLineTotalEnd)
                {
                    currentLine = context.Document.GetLineByOffset(elementOffset);
                    currentLineStart = currentLine.Offset;
                    currentLineEnd = currentLineStart + currentLine.Length;
                    currentLineTotalEnd = currentLineStart + currentLine.Length;
                    
                    RenderLine(currentLine);
                }
            }
        }
        currentLine = null;
        CurrentContext = null;
    }

    /// <summary>
    /// Override this method to colorize an individual document line.
    /// </summary>
    protected abstract void RenderLine(Line line);

    /// <summary>
    /// Changes a part of the current document line.
    /// </summary>
    /// <param name="startOffset">Start offset of the region to change</param>
    /// <param name="endOffset">End offset of the region to change</param>
    /// <param name="action">Action that changes an individual <see cref="VisualLineElement"/>.</param>
    protected void RenderLineRange(int startOffset, int endOffset, Action<VisualLineElement> action)
    {
        if (startOffset < currentLineStart || currentLineEnd < startOffset)
        {
            throw new ArgumentOutOfRangeException(nameof(startOffset), $"{startOffset} < {currentLineStart} or {currentLineEnd} < {startOffset}");
        }

        if (endOffset < startOffset || currentLineEnd < endOffset)
        {
            throw new ArgumentOutOfRangeException(nameof(endOffset), $"{endOffset} < {startOffset} or {currentLineEnd} < {endOffset}");
        }

        var line = CurrentContext!.VisualLine;
        var sCol = line.GetVisualColumn(startOffset - firstLineStart);
        var eCol = line.GetVisualColumn(endOffset - firstLineStart);
        
        if (sCol < eCol)
        {
            Render(sCol, eCol, action);
        }
    }
}
