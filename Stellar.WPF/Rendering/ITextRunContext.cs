using System.Windows.Media.TextFormatting;

using Stellar.WPF.Utilities;

namespace Stellar.WPF.Rendering;

/// <summary>
/// Context for creating a text run.
/// </summary>
public interface ITextRunContext
{
    /// <summary>
    /// The document from which to create a text run.
    /// </summary>
    Document.Document Document { get; }

    /// <summary>
    /// The text view from which to create a text run.
    /// </summary>
    //TextView TextView { get; }

    /// <summary>
    /// The visual line under construction.
    /// </summary>
    //VisualLine VisualLine { get; }

    /// <summary>
    /// Gets the global text run properties.
    /// </summary>
    TextRunProperties GlobalTextRunProperties { get; }

    /// <summary>
    /// A text segment from the document as a string.
    /// </summary>
    /// <remarks>
    /// This method returns a self-describing <see cref="StringSegment"/>, i.e., a text segment and metadata about it.
    /// It should be the preferred text access method in the text transformation pipeline to avoid repeated allocation
    /// of string instances for text within the same line.
    /// </remarks>
    StringSegment GetText(int offset, int length);
}