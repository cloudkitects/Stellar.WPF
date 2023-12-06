using System.Collections.Generic;

using Stellar.WPF.Utilities;

namespace Stellar.WPF.Styling.IO;

public class RuleSetDto
{
    public string? Name { get; set; }

    public IList<ElementDto>? Elements { get; set; } = new NullSafeCollection<ElementDto>();
}
