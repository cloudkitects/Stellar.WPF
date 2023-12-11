using System.Collections.Generic;

using Stellar.WPF.Utilities;

namespace Stellar.WPF.Styling;

internal class Syntax
{
    public string? Name { get; set; }

    public IList<string> Extensions { get; internal set; } = new NullSafeCollection<string>();

    public IList<Style> Styles { get; internal set; } = new NullSafeCollection<Style>();

    public IList<RuleSet> RuleSets { get; internal set; } = new NullSafeCollection<RuleSet>();
}
