using System.Collections.Generic;

using Stellar.WPF.Utilities;

namespace Stellar.WPF.Styling.IO;

/// <summary>
/// A syntax definition, as read from a YAML file.
/// </summary>
public class SyntaxDto
{ 
    public string? Name { get; set; }

    public IList<string> Extensions { get; internal set; } = new NullSafeCollection<string>();

    public IList<StyleDto> Styles { get; internal set; } = new NullSafeCollection<StyleDto>();

    public IList<ContextDto> RuleSets { get; internal set; } = new NullSafeCollection<ContextDto>();
}
