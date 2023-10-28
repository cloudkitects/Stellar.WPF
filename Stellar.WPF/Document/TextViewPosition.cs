using System;
using System.Globalization;

namespace Stellar.WPF.Document;

/// <summary>
/// Represents a text location with a visual column.
/// </summary>
public struct TextViewPosition : IEquatable<TextViewPosition>, IComparable<TextViewPosition>
{
	int line, column, visualColumn;
	bool isAtEndOfLine;

	/// <summary>
	/// Gets/Sets Location.
	/// </summary>
	public Location Location
    {
        readonly get => new(line, column);
        set
        {
            line = value.Line;
            column = value.Column;
        }
    }

    /// <summary>
    /// Gets/Sets the line number.
    /// </summary>
    public int Line
	{
		readonly get => line;
        set => line = value;
    }

    /// <summary>
    /// Gets/Sets the (text) column number.
    /// </summary>
    public int Column
	{
		readonly get => column;
        set => column = value;
    }

    /// <summary>
    /// Gets/Sets the visual column number.
    /// Can be -1 (meaning unknown visual column).
    /// </summary>
    public int VisualColumn
	{
		readonly get => visualColumn;
        set => visualColumn = value;
    }

    /// <summary>
    /// When word-wrap is enabled and a line is wrapped at a position where there is no space character;
    /// then both the end of the first TextLine and the beginning of the second TextLine
    /// refer to the same position in the document, and also have the same visual column.
    /// In this case, the IsAtEndOfLine property is used to distinguish between the two cases:
    /// the value <c>true</c> indicates that the position refers to the end of the previous TextLine;
    /// the value <c>false</c> indicates that the position refers to the beginning of the next TextLine.
    /// 
    /// If this position is not at such a wrapping position, the value of this property has no effect.
    /// </summary>
    public bool IsAtEndOfLine
	{
		readonly get => isAtEndOfLine;
        set => isAtEndOfLine = value;
    }

    /// <summary>
    /// Creates a new TextViewPosition instance.
    /// </summary>
    public TextViewPosition(int line, int column, int visualColumn)
	{
		this.line = line;
		this.column = column;
		this.visualColumn = visualColumn;
		
		isAtEndOfLine = false;
	}

	/// <summary>
	/// Creates a new TextViewPosition instance.
	/// </summary>
	public TextViewPosition(int line, int column)
		: this(line, column, -1)
	{
	}

	/// <summary>
	/// Creates a new TextViewPosition instance.
	/// </summary>
	public TextViewPosition(Location location, int visualColumn)
	{
		line = location.Line;
		column = location.Column;
		
		this.visualColumn = visualColumn;
		
		isAtEndOfLine = false;
	}

	/// <summary>
	/// Creates a new TextViewPosition instance.
	/// </summary>
	public TextViewPosition(Location location)
		: this(location, -1)
	{
	}

	/// <inheritdoc/>
	public override readonly string ToString()
	{
		return $"[TextViewPosition Line={line} Column={column} VisualColumn={visualColumn} IsAtEndOfLine={IsAtEndOfLine}]";
	}

	#region Equals and GetHashCode implementation
	// The code in this region is useful if you want to use this structure in collections.
	// If you don't need it, you can just remove the region and the ": IEquatable<Struct1>" declaration.

	/// <inheritdoc/>
	public override bool Equals(object? obj)
	{
        return obj is TextViewPosition position && Equals(position);
    }

	/// <inheritdoc/>
	public override int GetHashCode()
	{
		var hashCode = isAtEndOfLine ? 115817 : 0;

		unchecked {
			hashCode += 1000000007 * Line.GetHashCode();
			hashCode += 1000000009 * Column.GetHashCode();
			hashCode += 1000000021 * VisualColumn.GetHashCode();
		}
		
		return hashCode;
	}

	/// <summary>
	/// Equality test.
	/// </summary>
	public bool Equals(TextViewPosition other)
	{
		return Line == other.Line && Column == other.Column && VisualColumn == other.VisualColumn && IsAtEndOfLine == other.IsAtEndOfLine;
	}

	/// <summary>
	/// Equality test.
	/// </summary>
	public static bool operator ==(TextViewPosition left, TextViewPosition right)
	{
		return left.Equals(right);
	}

	/// <summary>
	/// Inequality test.
	/// </summary>
	public static bool operator !=(TextViewPosition left, TextViewPosition right)
	{
		return !(left.Equals(right)); // use operator == and negate result
	}
	#endregion

	/// <inheritdoc/>
	public int CompareTo(TextViewPosition other)
	{
		int r;
		
		if ((r = Location.CompareTo(other.Location)) != 0)
        {
            return r;
        }

		if ((r = visualColumn.CompareTo(other.visualColumn)) != 0)
        {
            return r;
        }

        if (isAtEndOfLine && !other.isAtEndOfLine)
        {
            return -1;
        }
        
		if (!isAtEndOfLine && other.isAtEndOfLine)
        {
            return 1;
        }

        return 0;
	}
}
