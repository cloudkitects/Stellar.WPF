namespace Stellar.WPF.Rendering;

/// <summary>
/// Allows <see cref="VisualLineGenerator"/>s, <see cref="IVisualLineTransformer"/>s and
/// <see cref="IBackgroundRenderer"/>s to be notified when they are added or removed from a text view.
/// </summary>
public interface ITextViewConnector
{
    /// <summary>
    /// Attach an object to a text view.
    /// </summary>
    void AttachTo(TextView textView);

    /// <summary>
    /// Detach an object from a text view.
    /// </summary>
    void DetachFrom(TextView textView);
}
