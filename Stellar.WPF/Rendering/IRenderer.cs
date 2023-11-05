using System.Collections.Generic;

namespace Stellar.WPF.Rendering;

/// <summary>
/// The visual lines rendering contract.
/// </summary>
public interface IRenderer
{
	/// <summary>
	/// Initialize a renderer with the specified text run context and a list of visual line elements.
	/// </summary>
	void Initialize(ITextRunContext context, IList<VisualLineElement> elements);
}
