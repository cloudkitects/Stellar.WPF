﻿using YamlDotNet.Serialization;

namespace Stellar.WPF.Styling.IO;

public class StyleDto : IAnchoredObject
{
    /// <summary>
    /// Gets/Sets the name of the style.
    /// </summary>
    [YamlIgnore]
    public string? Name { get; set; }

    /// <summary>
    /// Gets/sets the font family. Null if the style does not change the font style.
    /// </summary>
    public string? FontFamily { get; set; }

    /// <summary>
    /// Gets/sets the font size. Null if the style does not change the font style.
    /// </summary>
    public int? FontSize { get; set; }

    /// <summary>
    /// Gets/sets the font weight. Null if the style does not change the font weight.
    /// </summary>
    public string? FontWeight { get; set; }

    /// <summary>
    /// Gets/sets the font style. Null if the style does not change the font style.
    /// </summary>
    public string? FontStyle { get; set; }

    /// <summary>
    ///  Gets/sets the underline flag. Null if the underline status does not change the font style.
    /// </summary>
    public bool? Underline { get; set; }

    /// <summary>
    ///  Gets/sets the strikethrough flag. Null if the strikethrough status does not change the font style.
    /// </summary>
    public bool? Strikethrough { get; set; }
    /// <summary>
    /// Gets/sets the foreground color applied by the style.
    /// </summary>
    public string? Foreground { get; set; }

    /// <summary>
    /// Gets/sets the background color applied by the style.
    /// </summary>
    public string? Background { get; set; }
}
