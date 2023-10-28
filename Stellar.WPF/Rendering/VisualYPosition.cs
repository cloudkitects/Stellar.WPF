namespace Stellar.WPF.Rendering;

/// <summary>
/// All possible positions that VisualLine.GetPosition() can return.
/// </summary>
public enum VisualYPosition
{
	Top,

    /// <summary>
    /// Same as Top for a line containing regular text using the main font; below Top
    /// for a line with inline UI elements larger than the text.
    /// </summary>
    TextTop,

	Bottom,

	/// <summary>
	/// Between Top and Bottom.
	/// </summary>
	Middle,

    /// <summary>
    /// Same as Bottom for a line containing regular text using the main font; above Bottom
    /// for a line with inline UI elements larger than the text.
    /// </summary>
	TextBottom,
	
	/// <summary>
	/// Between TextTop and TextBottom.
	/// </summary>
	TextMiddle,
	
	/// <summary>
	/// The text's baseline.
	/// </summary>
	Baseline
}
