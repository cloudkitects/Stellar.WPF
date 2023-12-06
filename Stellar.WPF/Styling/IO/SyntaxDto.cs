using System.Collections.Generic;

using YamlDotNet.Serialization;

using Stellar.WPF.Utilities;

namespace Stellar.WPF.Styling.IO;

/// <summary>
/// A syntax definition, as read from a YAML file.
/// </summary>
public class SyntaxDto
{ 
    public string? Name { get; set; }

    public IList<string> Extensions { get; set; } = new NullSafeCollection<string>();

    public IList<StyleDto> Styles { get; set; } = new NullSafeCollection<StyleDto>();

    [YamlMember(Alias = "rules")]
    public IList<RuleSetDto> RuleSets { get; set; } = new NullSafeCollection<RuleSetDto>();
}
