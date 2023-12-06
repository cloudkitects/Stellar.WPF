using System.Collections.Generic;

using YamlDotNet.Serialization;

using Stellar.WPF.Utilities;

namespace Stellar.WPF.Styling.IO;

public class ElementDto
{
    public string? Keywords { get; set; }
    
    public string? Span { get; set; }

    public string? End { get; set; }

    public string? Rule { get; set; }

    public StyleDto? Style { get; set; }

    public bool CaseSensitive { get; set; }

    public bool Multiline { get; set; }

    //[YamlMember(Alias = "rules")]
    //public IList<RuleSetDto> RuleSets { get; private set; } = new NullSafeCollection<RuleSetDto>();
}