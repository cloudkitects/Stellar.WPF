using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using Stellar.WPF.Highlighting;

namespace Stellar.WPF.Utilities;

/// <summary>
/// A text writer that supports creating spans of highlighted text.
/// </summary>
abstract class StyledTextWriter : TextWriter
{
    /// <summary>
    /// Gets called by the RichTextWriter base class when a BeginSpan() method
    /// that is not overwritten gets called.
    /// </summary>
    protected abstract void BeginUnhandledSpan();

    /// <summary>
    /// Writes the RichText instance.
    /// </summary>
    public void Write(StyledText richText)
    {
        Write(richText, 0, richText.Length);
    }

    /// <summary>
    /// Writes the RichText instance.
    /// </summary>
    public virtual void Write(StyledText richText, int offset, int length)
    {
        // We have to use a TextWriter reference to invoke the virtual Write(string) method.
        // If we just call Write(richText.Text.Substring(...)) below, then the C# compiler invokes
        // the non-virtual Write(RichText) method due to RichText's implicit conversion from string.
        // That leads to an immediate, unconditional StackOverflowException!
        foreach (var section in richText.GetStyledSections(offset, length))
        {
            BeginSpan(section.Style!);

            Write(richText.Text.AsSpan(section.Offset, section.Length));
            
            EndSpan();
        }
    }

    /// <summary>
    /// Begin a colored span.
    /// </summary>
    public virtual void BeginSpan(Color foregroundColor)
    {
        BeginUnhandledSpan();
    }

    /// <summary>
    /// Begin a span with modified font weight.
    /// </summary>
    public virtual void BeginSpan(FontWeight fontWeight)
    {
        BeginUnhandledSpan();
    }

    /// <summary>
    /// Begin a span with modified font style.
    /// </summary>
    public virtual void BeginSpan(FontStyle fontStyle)
    {
        BeginUnhandledSpan();
    }

    /// <summary>
    /// Begin a span with modified font family.
    /// </summary>
    public virtual void BeginSpan(FontFamily fontFamily)
    {
        BeginUnhandledSpan();
    }

    /// <summary>
    /// Begin a highlighted span.
    /// </summary>
    public virtual void BeginSpan(Highlighting.Style highlightingColor)
    {
        BeginUnhandledSpan();
    }

    /// <summary>
    /// Begin a span that links to the specified URI.
    /// </summary>
    public virtual void BeginHyperlinkSpan(Uri uri)
    {
        BeginUnhandledSpan();
    }

    /// <summary>
    /// Marks the end of the current span.
    /// </summary>
    public abstract void EndSpan();

    /// <summary>
    /// Increases the indentation level.
    /// </summary>
    public abstract void Indent();

    /// <summary>
    /// Decreases the indentation level.
    /// </summary>
    public abstract void Unindent();
}
