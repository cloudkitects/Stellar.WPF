using System.Collections.Generic;

using Stellar.WPF.Utilities;

namespace Stellar.WPF.Styling.IO;

/// <summary>
/// A context DTO holds a collection of rules and provisions for importing registered contexts.
/// </summary>
public class ContextDto : IAnchoredObject
{
    public string? Name { get; set; }

    public string? Imports { get; set; }

    public IList<RuleDto> Rules { get; internal set; } = new NullSafeCollection<RuleDto>();
}
