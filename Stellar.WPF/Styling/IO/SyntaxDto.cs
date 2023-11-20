using System.Collections.Generic;

using Stellar.WPF.Utilities;

namespace Stellar.WPF.Styling.IO;

/// <summary>
/// A syntax definition, as read from a YAML file.
/// </summary>
public class SyntaxDto
{
    
    public string? Name { get; set; }

    public IList<string> Extensions { get; private set; }

    public IList<StyleDto> Styles { get; private set; }

    public SyntaxDto()
    {
        Styles = new NullSafeCollection<StyleDto>();
        Extensions = new NullSafeCollection<string>();
    }
}
