using System.Collections.Generic;

using YamlDotNet.Serialization;

using Stellar.WPF.Utilities;

namespace Stellar.WPF.Styling.IO;

public class RuleSetDto : IAnchoredObject
{
    [YamlIgnore]
    public string? Name { get; set; }

    public IList<RuleDto>? Rules { get; internal set; } = new NullSafeCollection<RuleDto>();
}
