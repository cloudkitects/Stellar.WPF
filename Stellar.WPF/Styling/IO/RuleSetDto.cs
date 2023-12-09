using System.Collections.Generic;

using Stellar.WPF.Utilities;

namespace Stellar.WPF.Styling.IO;

public class RuleSetDto : IAnchoredObject
{
    public string? Name { get; set; }

    public string? Import { get; set; }

    public IList<RuleDto>? Rules { get; internal set; } = new NullSafeCollection<RuleDto>();
}
