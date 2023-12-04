using System;

namespace Stellar.WPF.Utilities;


/// <summary>
/// Represents a string segment in a similar way as
/// System.ArraySegment&lt;T&gt; does for arrays.
/// </summary>
public readonly struct StringSegment : IEquatable<StringSegment>
{
    private readonly string text;
    private readonly int offset;
    private readonly int count;

    /// <summary>
    /// Creates a new StringSegment.
    /// </summary>
    public StringSegment(string text, int offset, int count)
    {
        this.text = text ?? throw new ArgumentNullException(nameof(text));

        if (offset < 0 || text.Length < offset)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }

        if (text.Length < offset + count)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }

        this.offset = offset;
        this.count = count;
    }

    /// <summary>
    /// Creates a new StringSegment.
    /// </summary>
    public StringSegment(string text)
    {
        this.text = text ?? throw new ArgumentNullException(nameof(text));
        
        offset = 0;
        count = text.Length;
    }

    /// <summary>
    /// Gets the string used for this segment.
    /// </summary>
    public string Text => text;

    /// <summary>
    /// Gets the start offset of the segment with the text.
    /// </summary>
    public int Offset => offset;

    /// <summary>
    /// Gets the length of the segment.
    /// </summary>
    public int Count => count;

    #region Equals and GetHashCode implementation
    /// <inheritdoc/>
    public override bool Equals(object obj)
    {
        if (obj is StringSegment segment)
        {
            return Equals(segment);
        }

        return false;
    }

    /// <inheritdoc/>
    public bool Equals(StringSegment other) => !(!ReferenceEquals(text, other.text) || offset != other.offset || count != other.count);

    /// <inheritdoc/>
    public override int GetHashCode() => text.GetHashCode() ^ offset ^ count;

    /// <summary>
    /// Equality operator.
    /// </summary>
    public static bool operator ==(StringSegment left, StringSegment right)
    {
        return left.Equals(right);
    }

    /// <summary>
    /// Inequality operator.
    /// </summary>
    public static bool operator !=(StringSegment left, StringSegment right)
    {
        return !left.Equals(right);
    }
    #endregion
}
