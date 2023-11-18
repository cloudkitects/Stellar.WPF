namespace Stellar.WPF.Document;

/// <summary>
/// A line inside a <see cref="IDocument"/>.
/// </summary>
public interface ILine : ISegment
{
    /// <summary>
    /// Gets the length of this line, including the line separator.
    /// </summary>
    int TextLength { get; }

    /// <summary>
    /// Gets the length of the line separator.
    /// Returns 1 or 2; or 0 at the end of the document.
    /// </summary>
    int SeparatorLength { get; }

    /// <summary>
    /// Gets the number of this line.
    /// The first line has the number 1.
    /// </summary>
    int Number { get; }

    /// <summary>
    /// Gets the previous line. Returns null if this is the first line in the document.
    /// </summary>
    ILine PreviousLine { get; }

    /// <summary>
    /// Gets the next line. Returns null if this is the last line in the document.
    /// </summary>
    ILine NextLine { get; }

    /// <summary>
    /// Gets whether the line was deleted.
    /// </summary>
    bool IsDeleted { get; }
}
