﻿using System.Collections.Generic;

using Stellar.WPF.Utilities;

namespace Stellar.WPF.Styling.IO;

/// <summary>
/// A syntax definition, as read from a YAML file.
/// </summary>
public class SyntaxDto
{
    
    public string? Name { get; set; }

    public IList<string> Extensions { get; private set; } = new NullSafeCollection<string>();

    public IList<StyleDto> Styles { get; private set; } = new NullSafeCollection<StyleDto>();

    public IList<RuleSetDto> RuleSets { get; private set; } = new NullSafeCollection<RuleSetDto>();
}
