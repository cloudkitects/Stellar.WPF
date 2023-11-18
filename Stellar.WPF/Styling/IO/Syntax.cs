using System.Collections.Generic;

using Stellar.WPF.Utilities;

namespace Stellar.WPF.Styling.IO;

/// <summary>
/// A syntax definition, as read from a YAML file.
/// </summary>
public class Syntax
{
    public string? Name { get; set; }

    public IList<string> Extensions { get; private set; }

    public IList<Style> Styles { get; private set; }

    public Syntax()
    {
        Styles = new NullSafeCollection<Style>();
        Extensions = new NullSafeCollection<string>();
    }
}
