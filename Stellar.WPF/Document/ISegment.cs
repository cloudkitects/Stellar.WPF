namespace Stellar.WPF.Document;

/// <summary>
/// A text segment defined by document start and end offsets plus length.
/// </summary>
public interface ISegment
{
    /// <summary>
    /// Gets the start offset of the segment.
    /// </summary>
    int Offset { get; }

    /// <summary>
    /// Gets the length of the segment.
    /// </summary>
    /// <remarks>Length does not include the line delimiter for line segments.</remarks>
    int Length { get; }

    /// <summary>
    /// Gets the end offset of the segment.
    /// </summary>
    /// <remarks>EndOffset = Offset + Length;</remarks>
    int EndOffset { get; }
}
