using System;
using System.IO;
using System.Net;

namespace Stellar.WPF.Styling;

/// <summary>
/// Holds options for converting text to HTML.
/// </summary>
public class HtmlOptions
{
    /// <summary>
    /// The amount of spaces a tab gets converted to.
    /// </summary>
    public int TabSize { get; set; }

    /// <summary>
    /// Creates a default HtmlOptions instance.
    /// </summary>
    public HtmlOptions()
    {
        TabSize = 4;
    }

    /// <summary>
    /// Creates a new HtmlOptions instance that copies applicable options from the <see cref="TextEditorOptions"/>.
    /// </summary>
    public HtmlOptions(TextEditorOptions options) : this()
    {
        TabSize = (options ?? throw new ArgumentNullException(nameof(options))).IndentationSize;
    }


    /// <summary>
    /// Writes the HTML attribute for the style to the text writer.
    /// </summary>
    public virtual void WriteStyleAttributeForColor(TextWriter writer, Style style)
    {
        (writer ?? throw new ArgumentNullException(nameof(writer))).Write(" style=\"");

        WebUtility.HtmlEncode((style ?? throw new ArgumentNullException(nameof(style))).ToCss(), writer);

        writer.Write('"');
    }

    /// <summary>
    /// Gets whether the color needs to be written out to HTML.
    /// </summary>
    public virtual bool ColorNeedsSpanForStyling(Style style)
    {
        return !string.IsNullOrEmpty((style ?? throw new ArgumentNullException(nameof(style))).ToCss());
    }
}
