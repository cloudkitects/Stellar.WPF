using System.Collections.Generic;

namespace Stellar.WPF.Rendering;

/// <summary>
/// Allows transforming visual line elements.
/// </summary>
public interface IVisualLineTransformer
{
	/// <summary>
	/// Applies the transformation to the specified list of visual line elements.
	/// </summary>
	void Transform(ITextRunContext context, IList<VisualLineElement> elements);
}
