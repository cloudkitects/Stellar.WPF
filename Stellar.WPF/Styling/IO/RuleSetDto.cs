using System.Collections.Generic;

using YamlDotNet.Serialization;

using Stellar.WPF.Utilities;

namespace Stellar.WPF.Styling.IO;

public class RuleSetDto
{
    [YamlIgnore]
    public string? Name { get; set; }

    public IList<ElementDto>? Elements { get; internal set; } = new NullSafeCollection<ElementDto>();
}
