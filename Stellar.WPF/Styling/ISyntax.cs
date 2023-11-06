using System.Collections.Generic;

namespace Stellar.WPF.Styling;

/// <summary>
/// A syntax-highlighting (styling) definition.
/// </summary>
///[TypeConverter(typeof(HighlightingDefinitionTypeConverter))]
public interface ISyntax
{
    /// <summary>
    /// The syntax name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// The syntax rule set.
    /// </summary>
    RuleSet RuleSet { get; }

    /// <summary>
    /// Get a rule set by name.
    /// <see langword="TODO"></see>: this is implemented in &lt;string, ISyntax&gt; dictionaries, consider deprecating.
    /// </summary>
    /// <returns>The rule set, or null if it is not found.</returns>
    RuleSet GetNamedRuleSet(string name);

    /// <summary>
    /// Get a style by name.
    /// </summary>
    /// <returns>The highlighting color, or null if it is not found.</returns>
    Style GetStyle(string name);

    /// <summary>
    /// Get a list of all named styles.
    /// </summary>
    IEnumerable<Style> NamedStyles { get; }

    /// <summary>
    /// A list of the syntax properties.
    /// </summary>
    IDictionary<string, string> Properties { get; }
}
