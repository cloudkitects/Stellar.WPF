using System;
using System.ComponentModel;
using System.Globalization;

namespace Stellar.WPF.Document;

/// <summary>
/// A 1-based line/column position.
/// </summary>
/// <remarks>
/// The document provides the methods <see cref="IDocument.GetLocation"/> and
/// <see cref="IDocument.GetOffset(Location)"/> to convert between offsets and TextLocations.
/// </remarks>
[Serializable]
[TypeConverter(typeof(LocationConverter))]
public readonly struct Location : IComparable<Location>, IEquatable<Location>
{
    /// <summary>
    /// Represents no text location (0, 0).
    /// </summary>
    public static readonly Location Empty = new(0, 0);

    /// <summary>
    /// Creates a TextLocation instance.
    /// </summary>
    public Location(int line, int column)
    {
        this.line = line;
        this.column = column;
    }

    private readonly int column, line;

    /// <summary>
    /// Gets the line number.
    /// </summary>
    public int Line => line;

    /// <summary>
    /// Gets the column number.
    /// </summary>
    public int Column => column;

    /// <summary>
    /// Gets whether the TextLocation instance is empty.
    /// </summary>
    public bool IsEmpty => column <= 0 && line <= 0;

    /// <summary>
    /// Gets a string representation for debugging purposes.
    /// </summary>
    public override string ToString() => string.Format(CultureInfo.InvariantCulture, "(Line {1}, Col {0})", column, line);

    /// <summary>
    /// Gets a hash code.
    /// </summary>
    public override int GetHashCode() => unchecked(191 * column.GetHashCode() ^ line.GetHashCode());

    /// <summary>
    /// Equality test.
    /// </summary>
    public override bool Equals(object? obj) => obj is Location other && this == other;

    /// <summary>
    /// Equality test.
    /// </summary>
    public bool Equals(Location other) => this == other;

    /// <summary>
    /// Equality test.
    /// </summary>
    public static bool operator ==(Location a, Location b)
    {
        return a.column == b.column && a.line == b.line;
    }

    /// <summary>
    /// Inequality test.
    /// </summary>
    public static bool operator !=(Location a, Location b)
    {
        return a.column != b.column || a.line != b.line;
    }

    /// <summary>
    /// Compares two text locations.
    /// </summary>
    public static bool operator <(Location a, Location b)
    {
        return a.line < b.Line || (a.line == b.line && a.column < b.column);
    }

    /// <summary>
    /// Compares two text locations.
    /// </summary>
    public static bool operator >(Location a, Location b)
    {
        return a.line > b.line || (a.line == b.line && a.column > b.column);
    }

    /// <summary>
    /// Compares two text locations.
    /// </summary>
    public static bool operator <=(Location a, Location b)
    {
        return !(a > b);
    }

    /// <summary>
    /// Compares two text locations.
    /// </summary>
    public static bool operator >=(Location a, Location b)
    {
        return !(a < b);
    }

    /// <summary>
    /// Compares two text locations.
    /// </summary>
    public int CompareTo(Location other)
    {
        return this == other
            ? 0
            : this < other
                ? -1
                : +1;
    }
}
