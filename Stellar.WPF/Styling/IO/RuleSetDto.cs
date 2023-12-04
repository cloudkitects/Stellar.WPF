using System.Collections.Generic;

using Stellar.WPF.Utilities;

namespace Stellar.WPF.Styling.IO;

public class RuleSetDto
{
    /// <summary>
    /// The optional name of the rule set.
    /// </summary>
    public string? Name { get; set; }

    private readonly NullSafeCollection<ElementDto> elements = new();

    /// <summary>
    /// Gets the collection of elements.
    /// </summary>
    public IList<ElementDto> Elements => elements;
}
