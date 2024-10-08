﻿namespace Stellar.WPF.Document;

/// <summary>
/// An (Offset,Length)-pair.
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
    /// <remarks>For line segments (IDocumentLine), the length does not include the line delimiter.</remarks>
    int Length { get; }

    /// <summary>
    /// Gets the end offset of the segment.
    /// </summary>
    /// <remarks>EndOffset = Offset + Length;</remarks>
    int EndOffset { get; }
}
