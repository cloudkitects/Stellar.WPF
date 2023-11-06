using System;
using System.Text.RegularExpressions;

namespace Stellar.WPF.Styling;

/// <summary>
/// A highlighting rule.
/// </summary>
[Serializable]
public class Rule
{
    /// <summary>
    /// The rule's regular expression.
    /// </summary>
    public Regex Regex { get; set; }

    /// <summary>
    /// The rule's style.
    /// </summary>
    public Style Style { get; set; }
    
    /// <inheritdoc/>
    public override string ToString()
    {
        return $"[{GetType().Name} {Style?.Name ?? "<unnamed>"} {Regex}]";
    }
}
