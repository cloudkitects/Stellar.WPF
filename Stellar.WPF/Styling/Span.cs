using System.Text.RegularExpressions;

namespace Stellar.WPF.Styling;

/// <summary>
/// A text region defined by start and end expressions with styles,
/// e.g., <see langword="&lt;p&gt;"/>Hello, world.<see langword="&lt;/p&gt;"/>.
/// </summary>
public class Span
{
    /// <summary>
    /// The rule's regular expression.
    /// </summary>
    public Regex? Regex { get; set; }

    /// <summary>
    /// The rule's style.
    /// </summary>
    public Style? Style { get; set; }
    
    /// <summary>
    /// Gets the color used for the text matching the start expression.
    /// </summary>
    public Style StartStyle { get; set; } = new();

    /// <summary>
    /// Gets/Sets whether the span color includes the start.
    /// The default is <c>false</c>.
    /// </summary>
    public bool StyleIncludesStart { get; set; }

    /// <summary>
    /// Gets/Sets whether the span color includes the end.
    /// The default is <c>false</c>.
    /// </summary>
    public bool StyleIncludesEnd { get; set; }

    /// <summary>
    /// Gets/Sets the end expression.
    /// </summary>
    public Regex? EndRegex { get; set; }

    /// <summary>
    /// Gets the color used for the text matching the end expression.
    /// </summary>
    public Style EndStyle { get; set; } = new();

    /// <summary>
    /// Gets/Sets the rule set that applies inside this span.
    /// </summary>
    public RuleSet RuleSet { get; set; } = new();

    /// <inheritdoc/>
    public override string ToString()
    {
        return $"[{GetType().Name} Start=\"{Regex}\", End=\"{EndRegex}\"]";
    }
}