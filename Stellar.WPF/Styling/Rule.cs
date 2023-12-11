using System.Text.RegularExpressions;

namespace Stellar.WPF.Styling;

/// <summary>
/// A styling rule.
/// </summary>
public class Rule
{
    /// <summary>
    /// The rule's regular expression.
    /// </summary>
    public Regex? Regex { get; set; }

    /// <summary>
    /// The rule's style.
    /// </summary>
    public Style? Style { get; set; }
    
    /// <inheritdoc/>
    public override string ToString()
    {
        return $"[{GetType().Name} Regex={Regex} {Style?.Name ?? "<unnamed>"}]";
    }
}
