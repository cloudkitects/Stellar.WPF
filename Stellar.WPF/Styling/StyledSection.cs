using Stellar.WPF.Document;

namespace Stellar.WPF.Styling;

/// <summary>
/// A text segment with styling information.
/// </summary>
public class StyledSection : ISegment
{
    /// <summary>
    /// The section's offset in the document.
    /// </summary>
    public int Offset { get; set; }

    /// <summary>
    /// The length of the section.
    /// </summary>
    public int Length { get; set; }

    int ISegment.EndOffset
    {
        get { return Offset + Length; }
    }

    /// <summary>
    /// Gets the style associated with this styled section.
    /// </summary>
    public Style? Style { get; set; }

    /// <inheritdoc/>
    public override string ToString()
    {
        return string.Format($"[StyledSection ({Offset}-{Offset + Length})={Style}]");
    }
}
