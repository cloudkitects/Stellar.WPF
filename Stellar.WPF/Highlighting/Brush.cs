using System;
using System.Windows.Media;

using Stellar.WPF.Rendering;

namespace Stellar.WPF.Highlighting;

/// <summary>
/// A syntax highlighting brush wrapping a system brush.
/// </summary>
[Serializable]
public abstract class Brush
{
	/// <summary>
	/// Gets the real brush.
	/// </summary>
	/// <param name="context">The construction context. context can be null!</param>
	public abstract System.Windows.Media.Brush GetBrush(ITextRunContext context);

	/// <summary>
	/// Gets the color of the brush.
	/// </summary>
	/// <param name="context">The construction context. context can be null!</param>
	public virtual Color? GetColor(ITextRunContext context)
	{
		var brush = GetBrush(context) as SolidColorBrush;
        
		return brush?.Color;
    }
}
