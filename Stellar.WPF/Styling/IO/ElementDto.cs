﻿using YamlDotNet.Serialization;

namespace Stellar.WPF.Styling.IO;

public class ElementDto
{
    [YamlMember(DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
    public string? Keywords { get; set; }

    [YamlMember(DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
    public string? Span { get; set; }

    [YamlMember(DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
    public string? End { get; set; }

    [YamlMember(DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
    public string? Rule { get; set; }

    [YamlMember(DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
    public StyleDto? Style { get; set; }

    [YamlMember(DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
    public bool? CaseSensitive { get; set; }

    [YamlMember(DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
    public bool? Multiline { get; set; }
}