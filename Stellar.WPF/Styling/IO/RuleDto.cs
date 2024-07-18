using System.Collections.Generic;

using YamlDotNet.Serialization;

namespace Stellar.WPF.Styling.IO;

public class RuleDto
{
    [YamlMember(DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
    public string? Keywords { get; set; }

    [YamlMember(DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
    public string? Span { get; set; }

    [YamlMember(DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
    public string? Stop { get; set; }

    [YamlMember(DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
    public string? Rule { get; set; }

    [YamlMember(DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
    public bool? IgnoreCase { get; set; }

    [YamlMember(DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
    public bool? Multiline { get; set; }

    [YamlMember(DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
    public IList<RuleSetDto>? RuleSets { get; set; }

    [YamlMember(DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
    public StyleDto? Style { get; set; }
}