using System;
using System.IO;

namespace Stellar.WPF.Document;

/// <summary>
/// A read-only view on a (potentially mutable) text source.
/// </summary>
public interface ITextSource
{
    /// <summary>
    /// A checkpoint for this text source, null for unversioned text sources.
    /// </summary>
    ICheckpoint? Checkpoint { get; }

    /// <summary>
    /// Create an immutable snapshot of this text source, the only thread-safe method in this interface.
    /// </summary>
    ITextSource CreateSnapshot();

    /// <summary>
    /// Create an immutable snapshot of a section of this text source (thread-safe).
    /// </summary>
    ITextSource CreateSnapshot(int offset, int length);

    /// <summary>
    /// Creates a new TextReader from this text source.
    /// </summary>
    TextReader CreateReader();

    /// <summary>
    /// Creates a new TextReader from a section of this text source.
    /// </summary>
    TextReader CreateReader(int offset, int length);

    /// <summary>
    /// The total text length.
    /// </summary>
    /// <returns>The length of the text, in characters.</returns>
    /// <remarks>Faster than Text.Length as it doesn't create a String object.</remarks>
    int TextLength { get; }

    /// <summary>
    /// The whole text.
    /// </summary>
    string Text { get; }

    /// <summary>
    /// Gets a character at the specified position in the document.
    /// </summary>
    /// <paramref name="offset">The index of the character to get.</paramref>
    /// <exception cref="ArgumentOutOfRangeException">Offset is outside the valid range (0 to TextLength - 1).</exception>
    /// <returns>The character at the specified position.</returns>
    /// <remarks>Faster than Text[offset] as it doesn't create a String object.</remarks>
    char GetCharAt(int offset);

    /// <summary>
    /// Retrieves the text for a section of the document.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">offset or length is outside the valid range.</exception>
    /// <remarks>Faster than Text.Substring as it doesn't create a String object.</remarks>
    string GetText(int offset, int length);

    /// <summary>
    /// Retrieves the text for the given segment.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">offset or length is outside the valid range.</exception>
    string GetText(ISegment segment);

    /// <summary>
    /// Writes the text from this document into the TextWriter.
    /// </summary>
    void WriteTextTo(TextWriter writer);

    /// <summary>
    /// Writes the text from this document into the TextWriter.
    /// </summary>
    void WriteTextTo(TextWriter writer, int offset, int length);

    /// <summary>
    /// Gets the index of the first occurrence of the character in the specified array.
    /// </summary>
    /// <param name="c">Character to search for</param>
    /// <param name="startIndex">Start index of the area to search.</param>
    /// <param name="count">Length of the area to search.</param>
    /// <returns>The first index where the character was found; or -1 if no occurrence was found.</returns>
    int IndexOf(char c, int startIndex, int count);

    /// <summary>
    /// Gets the index of the first occurrence of any character in the specified array.
    /// </summary>
    /// <param name="anyOf">Characters to search for</param>
    /// <param name="startIndex">Start index of the area to search.</param>
    /// <param name="count">Length of the area to search.</param>
    /// <returns>The first index where any character was found; or -1 if no occurrence was found.</returns>
    int IndexOfAny(char[] anyOf, int startIndex, int count);

    /// <summary>
    /// Gets the index of the first occurrence of the specified search text in this text source.
    /// </summary>
    /// <param name="searchText">The search text</param>
    /// <param name="startIndex">Start index of the area to search.</param>
    /// <param name="count">Length of the area to search.</param>
    /// <param name="comparisonType">String comparison to use.</param>
    /// <returns>The first index where the search term was found; or -1 if no occurrence was found.</returns>
    int IndexOf(string searchText, int startIndex, int count, StringComparison comparisonType);

    /// <summary>
    /// Gets the index of the last occurrence of the specified character in this text source.
    /// </summary>
    /// <param name="c">The search character</param>
    /// <param name="startIndex">Start index of the area to search.</param>
    /// <param name="count">Length of the area to search.</param>
    /// <returns>The last index where the search term was found; or -1 if no occurrence was found.</returns>
    /// <remarks>The search proceeds backwards from (startIndex+count) to startIndex.
    /// This is different than the meaning of the parameters on string.LastIndexOf!</remarks>
    int LastIndexOf(char c, int startIndex, int count);

    /// <summary>
    /// Gets the index of the last occurrence of the specified search text in this text source.
    /// </summary>
    /// <param name="searchText">The search text</param>
    /// <param name="startIndex">Start index of the area to search.</param>
    /// <param name="count">Length of the area to search.</param>
    /// <param name="comparisonType">String comparison to use.</param>
    /// <returns>The last index where the search term was found; or -1 if no occurrence was found.</returns>
    /// <remarks>The search proceeds backwards from (startIndex+count) to startIndex.
    /// This is different than the meaning of the parameters on string.LastIndexOf!</remarks>
    int LastIndexOf(string searchText, int startIndex, int count, StringComparison comparisonType);
}
