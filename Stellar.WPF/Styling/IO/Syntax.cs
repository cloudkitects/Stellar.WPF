using System.Collections.Generic;

using Stellar.WPF.Utilities;

namespace Stellar.WPF.Styling.IO;

internal class Syntax
{
    public string? Name { get; set; }

    public IList<string> Extensions { get; internal set; } = new NullSafeCollection<string>();

    public IList<Style> Styles { get; internal set; } = new NullSafeCollection<Style>();

    //public IList<RuleSetDto> RuleSets { get; internal set; } = new NullSafeCollection<RuleSetDto>();

    public Syntax(SyntaxDto dto)
    {
        Name = dto.Name;
        Extensions = dto.Extensions;

        foreach(var style in dto.Styles)
        {
            Styles.Add(new Style(style));
        }
    }
}
