using System.Collections.Generic;

namespace Stellar.WPF.Styling;

/// <summary>
/// A syntax-highlighting (styling) definition.
/// </summary>
public interface ISyntax
{
    /// <summary>
    /// The syntax name.
    /// </summary>
    string? Name { get; }

    /// <summary>
    /// The syntax rule set.
    /// </summary>
    Context RuleSet { get; }
}
