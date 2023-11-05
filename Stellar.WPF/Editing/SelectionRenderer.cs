// Copyright (c) 2014 AlphaSierraPapa for the SharpDevelop Team
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy of this
// software and associated documentation files (the "Software"), to deal in the Software
// without restriction, including without limitation the rights to use, copy, modify, merge,
// publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons
// to whom the Software is furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all copies or
// substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
// INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR
// PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE
// FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR
// OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.

using System;

using Stellar.WPF.Rendering;

namespace Stellar.WPF.Editing;

internal sealed class SelectionRenderer : Renderer
{
    private readonly TextArea textArea;

    public SelectionRenderer(TextArea textArea)
    {
        this.textArea = textArea ?? throw new ArgumentNullException(nameof(textArea));
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Leaving the selection untouched is an option. 
    /// </remarks>
    protected override void Render(ITextRunContext context)
    {
        if (textArea.SelectionForeground is null)
        {
            return;
        }

        var s = context.VisualLine.FirstLine.Offset;
        var e = context.VisualLine.LastLine.Offset + context.VisualLine.LastLine.Length;

        foreach (var segment in textArea.Selection.Segments)
        {
            var ss = segment.StartOffset;
            var se = segment.EndOffset;
            
            if (se <= s)
            {
                continue;
            }

            if (ss >= e)
            {
                continue;
            }

            var sc = ss < s
                ? 0
                : context.VisualLine.ValidateVisualColumn(ss, segment.StartVisualColumn, textArea.Selection.EnableVirtualSpace);

            var ec = se > e
                ? textArea.Selection.EnableVirtualSpace ? int.MaxValue : context.VisualLine.VisualLengthWithEndOfLineMarker
                : context.VisualLine.ValidateVisualColumn(se, segment.EndVisualColumn, textArea.Selection.EnableVirtualSpace);

            Render(sc, ec, element => { element.TextRunProperties!.SetForegroundBrush(textArea.SelectionForeground); });
        }
    }
}
