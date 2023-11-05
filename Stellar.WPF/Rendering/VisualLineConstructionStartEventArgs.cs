using System;

using Stellar.WPF.Document;

namespace Stellar.WPF.Rendering;

/// <summary>
/// EventArgs for the <see cref="TextView.VisualLineConstructionStarting"/> event.
/// </summary>
public class VisualLineConstructionStartEventArgs : EventArgs
{
    /// <summary>
    /// Gets/Sets the first line that is visible in the TextView.
    /// </summary>
    public Line FirstLineInView { get; private set; }

    /// <summary>
    /// Creates a new VisualLineConstructionStartEventArgs instance.
    /// </summary>
    public VisualLineConstructionStartEventArgs(Line firstLineInView)
    {
        FirstLineInView = firstLineInView ?? throw new ArgumentNullException(nameof(firstLineInView));
    }
}