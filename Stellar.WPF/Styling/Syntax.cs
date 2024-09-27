using System.Collections.Generic;

using Stellar.WPF.Utilities;

namespace Stellar.WPF.Styling;

internal class Syntax : ISyntax
{
    private readonly List<Context> ruleSets = new();

    public Syntax(IO.SyntaxDto dto, ISyntaxResolver resolver)
    {
        Name = dto.Name;
        Extensions = dto.Extensions;
        
        foreach (var ruleSet in dto.RuleSets)
        {
            ruleSets.Add(IO.Loader.LoadRuleSet(ruleSet));
        }
    }

    public string? Name { get; set; }

    public IList<string> Extensions { get; internal set; } = new NullSafeCollection<string>();

    public IList<Context> RuleSets => ruleSets;

    public Context RuleSet => throw new System.NotImplementedException();
}
