using System;

namespace Stellar.WPF.Rendering;

/// <summary>
/// Abstract base class for generators that produce new visual line elements.
/// </summary>
public abstract class VisualLineElementGenerator
{
	/// <summary>
	/// Gets the text run construction context.
	/// </summary>
	protected ITextRunContext? Context { get; private set; }

	/// <summary>
	/// Initializes the generator for the <see cref="ITextRunContext"/>
	/// </summary>
	public virtual void StartGeneration(ITextRunContext context)
	{
        Context = context ?? throw new ArgumentNullException(nameof(context));
	}

	/// <summary>
	/// De-initializes the generator.
	/// </summary>
	public virtual void FinishGeneration()
	{
		Context = null;
	}

	/// <summary>
	/// Should only be used by VisualLine.ConstructVisualElements.
	/// </summary>
	internal int cachedInterest;

	/// <summary>
	/// Gets the first offset >= startOffset where the generator wants to construct an element.
	/// Return -1 to signal no interest.
	/// </summary>
	public abstract int GetFirstInterestedOffset(int startOffset);

	/// <summary>
	/// Constructs an element at the specified offset.
	/// May return null if no element should be constructed.
	/// </summary>
	/// <remarks>
	/// Avoid signalling interest and then building no element by returning null - doing so
	/// causes the generated <see cref="VisualLineText"/> elements to be unnecessarily split
	/// at the position where you signalled interest.
	/// </remarks>
	public abstract VisualLineElement ConstructElement(int offset);
}

