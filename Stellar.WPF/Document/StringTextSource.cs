using System;
using System.IO;

namespace Stellar.WPF.Document;

/// <summary>
/// Implements the ITextSource interface using a string.
/// </summary>
[Serializable]
public class StringTextSource : ITextSource
{
    /// <summary>
    /// Gets a text source containing the empty string.
    /// </summary>
    public static readonly StringTextSource Empty = new(string.Empty);

    private readonly string text;
    private readonly ICheckpoint? checkpoint;

    /// <summary>
    /// Creates a new StringTextSource with the given text.
    /// </summary>
    public StringTextSource(string text)
    {
        this.text = text ?? throw new ArgumentNullException(nameof(text));
    }

    /// <summary>
    /// Creates a new StringTextSource with the given text.
    /// </summary>
    public StringTextSource(string text, ICheckpoint checkpoint)
    {
        this.text = text ?? throw new ArgumentNullException(nameof(text));
        this.checkpoint = checkpoint;
    }

    /// <inheritdoc/>
    public ICheckpoint? Checkpoint => checkpoint;

    /// <inheritdoc/>
    public int TextLength => text.Length;

    /// <inheritdoc/>
    public string Text => text;

    /// <inheritdoc/>
    public ITextSource CreateSnapshot() => this; // StringTextSource is immutable

    /// <inheritdoc/>
    public ITextSource CreateSnapshot(int offset, int length) => new StringTextSource(text.Substring(offset, length));

    /// <inheritdoc/>
    public TextReader CreateReader() => new StringReader(text);

    /// <inheritdoc/>
    public TextReader CreateReader(int offset, int length) => new StringReader(text.Substring(offset, length));

    /// <inheritdoc/>
    public void WriteTextTo(TextWriter writer) => writer.Write(text);

    /// <inheritdoc/>
    public void WriteTextTo(TextWriter writer, int offset, int length) => writer.Write(text.Substring(offset, length));

    /// <inheritdoc/>
    public char GetCharAt(int offset) => text[offset];

    /// <inheritdoc/>
    public string GetText(int offset, int length) => text.Substring(offset, length);

    /// <inheritdoc/>
    public string GetText(ISegment segment)
    {
        if (segment == null)
        {
            throw new ArgumentNullException(nameof(segment));
        }

        return text.Substring(segment.Offset, segment.Length);
    }

    /// <inheritdoc/>
    public int IndexOf(char c, int startIndex, int count) => text.IndexOf(c, startIndex, count);

    /// <inheritdoc/>
    public int IndexOfAny(char[] anyOf, int startIndex, int count) => text.IndexOfAny(anyOf, startIndex, count);

    /// <inheritdoc/>
    public int IndexOf(string searchText, int startIndex, int count, StringComparison comparisonType) => text.IndexOf(searchText, startIndex, count, comparisonType);

    /// <inheritdoc/>
    public int LastIndexOf(char c, int startIndex, int count) => text.LastIndexOf(c, startIndex + count - 1, count);

    /// <inheritdoc/>
    public int LastIndexOf(string searchText, int startIndex, int count, StringComparison comparisonType) => text.LastIndexOf(searchText, startIndex + count - 1, count, comparisonType);
}
