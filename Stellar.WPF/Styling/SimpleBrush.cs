using System;
using System.Globalization;
using System.Runtime.Serialization;
using System.Windows.Media;

using Stellar.WPF.Rendering;

namespace Stellar.WPF.Styling;

/// <summary>
/// Highlighting brush implementation that takes a frozen brush.
/// </summary>
[Serializable]
public sealed class SimpleBrush : Brush, ISerializable
{
	readonly SolidColorBrush brush;

	internal SimpleBrush(SolidColorBrush brush)
	{
		brush.Freeze();
		this.brush = brush;
	}

	/// <summary>
	/// Creates a new HighlightingBrush with the specified color.
	/// </summary>
	public SimpleBrush(Color color) : this(new SolidColorBrush(color)) { }

    /// <summary>
    /// Creates a new HighlightingBrush with the specified color.
    /// </summary>
    public SimpleBrush(string color) : this((Color)ColorConverter.ConvertFromString(color)) { }

    private SimpleBrush(SerializationInfo info, StreamingContext context)
    {
        brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(info.GetString("color")));
        brush.Freeze();
    }

    /// <inheritdoc/>
    public override System.Windows.Media.Brush GetBrush(ITextRunContext context) => brush;

    /// <inheritdoc/>
    public override string ToString() => brush.ToString();


    void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context) => info.AddValue("color", brush.Color.ToString(CultureInfo.InvariantCulture));

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is SimpleBrush other && brush.Color.Equals(other.brush.Color);

    /// <inheritdoc/>
    public override int GetHashCode() => brush.Color.GetHashCode();
}

