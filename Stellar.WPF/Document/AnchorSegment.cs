using System;
using System.Globalization;

namespace Stellar.WPF.Document;

/// <summary>
/// A segment using <see cref="Anchor"/>s as start and end positions.
/// </summary>
/// <remarks>
/// For the constructors creating new anchors, the start position will be AfterInsertion and the end position will be BeforeInsertion.
/// Should the end position move before the start position, the segment will have length 0.
/// </remarks>
/// <seealso cref="ISegment"/>
/// <seealso cref="Segment"/>
public sealed class AnchorSegment : ISegment
{
    private readonly Anchor start, end;

    /// <inheritdoc/>
    public int Offset
    {
        get { return start.Offset; }
    }

    /// <inheritdoc/>
    public int Length
    {
        get
        {
            return Math.Max(0, end.Offset - start.Offset);
        }
    }

    /// <inheritdoc/>
    public int EndOffset
    {
        get
        {
            return Math.Max(start.Offset, end.Offset);
        }
    }

    /// <summary>
    /// Creates a new AnchorSegment using the specified anchors.
    /// The anchors must have <see cref="Anchor.SurvivesDeletion"/> set to true.
    /// </summary>
    public AnchorSegment(Anchor start, Anchor end)
    {
        if (start is null)
        {
            throw new ArgumentNullException(nameof(start));
        }

        if (end is null)
        {
            throw new ArgumentNullException(nameof(end));
        }

        if (!start.SurvivesDeletion)
        {
            throw new ArgumentException("Anchor segment sart anchor must survive deletion", nameof(start));
        }

        if (!end.SurvivesDeletion)
        {
            throw new ArgumentException("Anchor segment end anchor must survive deletion", nameof(end));
        }

        this.start = start;
        this.end = end;
    }

    /// <summary>
    /// Creates a new AnchorSegment that creates new anchors.
    /// </summary>
    public AnchorSegment(Document document, ISegment segment)
        : this(document, (segment ?? throw new ArgumentNullException(nameof(segment))).Offset, segment.Length)
    {
    }

    /// <summary>
    /// Creates a new AnchorSegment that creates new anchors.
    /// </summary>
    public AnchorSegment(Document document, int offset, int length)
    {
        if (document is null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        start = document.CreateAnchor(offset);
        start.SurvivesDeletion = true;
        start.MovementType = AnchorMovementType.AfterInsertion;

        end = document.CreateAnchor(offset + length);
        end.SurvivesDeletion = true;
        end.MovementType = AnchorMovementType.BeforeInsertion;
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return "[Offset=" + Offset.ToString(CultureInfo.InvariantCulture) + ", EndOffset=" + EndOffset.ToString(CultureInfo.InvariantCulture) + "]";
    }
}