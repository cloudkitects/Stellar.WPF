using System.Collections.Generic;

using YamlDotNet.Serialization;

namespace Stellar.WPF.Styling.IO;

/// <summary>
/// A rule DTO.
/// </summary>
/// <remarks>
/// Everything's declared optional so that it can be omitted when serializing,
/// but the loader will throw for invalid constructs. Either Keywords, Span or
/// Rule must be defined, and Style is required (and YAML.net has no such priovisions).
/// </remarks>
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
    public IList<ContextDto>? RuleSets { get; set; }

    [YamlMember(DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
    public StyleDto Style { get; set; } = new();
}