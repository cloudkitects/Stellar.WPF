using System;
using System.Diagnostics;
using System.Globalization;

namespace Stellar.WPF.Document;

/// <summary>
/// Represents a simple segment (Offset,Length pair) that is not automatically updated
/// on document changes.
/// </summary>
internal readonly struct SimpleSegment : IEquatable<SimpleSegment>, ISegment
{
    public static readonly SimpleSegment Invalid = new(-1, -1);

    public readonly int Offset, Length;

    int ISegment.Offset => Offset;

    int ISegment.Length => Length;

    public int EndOffset => Offset + Length;

    public SimpleSegment(int offset, int length)
    {
        Offset = offset;
        Length = length;
    }

    public SimpleSegment(ISegment segment)
    {
        Debug.Assert(segment != null);

        Offset = segment.Offset;
        Length = segment.Length;
    }

    /// <summary>
    /// The overlap of two segments or Invalid if they don't overlap.
    /// </summary>
    public static SimpleSegment GetOverlap(ISegment segment1, ISegment segment2)
    {
        var ss = Math.Max(segment1.Offset, segment2.Offset);
        var se = Math.Min(segment1.EndOffset, segment2.EndOffset);

        return se >= ss
            ? new SimpleSegment(ss, se - ss)
            : Invalid;
    }

    public override int GetHashCode()
    {
        unchecked
        {
            return Offset + 10301 * Length;
        }
    }

    public override bool Equals(object? obj)
    {
        return obj is SimpleSegment segment && Equals(segment);
    }

    public bool Equals(SimpleSegment other)
    {
        return Offset == other.Offset && Length == other.Length;
    }

    public static bool operator ==(SimpleSegment left, SimpleSegment right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(SimpleSegment left, SimpleSegment right)
    {
        return !left.Equals(right);
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return $"[{GetType().Name} Offset={Offset}, Length={Length}]";
    }
}

