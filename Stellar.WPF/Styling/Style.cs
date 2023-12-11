using System;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Media;

using Stellar.WPF.Styling.IO;
using Stellar.WPF.Utilities;

namespace Stellar.WPF.Styling;

/// <summary>
/// A named set of font properties and brushes.
/// </summary>
public class Style : IFreezable, ICloneable, IEquatable<Style>
{
    #region fields and props
    internal static readonly Style Empty = new Style().Freeze<Style>();
    private string name = string.Empty;
    private FontFamily? fontFamily = null;
    private int? fontSize;
    private FontWeight? fontWeight;
    private FontStyle? fontStyle;
    private bool? underline;
    private bool? strikethrough;
    private Brush? foreground;
    private Brush? background;
    private bool frozen;

    /// <summary>
    /// Gets/Sets the name of the style.
    /// </summary>
    public string Name
    {
        get => name;
        set
        {
            if (frozen)
            {
                throw new InvalidOperationException();
            }

            name = value;
        }
    }

    /// <summary>
    /// Gets/sets the font family. Null if the style does not change the font style.
    /// </summary>
    public FontFamily FontFamily
    {
        get => fontFamily!;
        set
        {
            if (frozen)
            {
                throw new InvalidOperationException();
            }

            fontFamily = value;
        }
    }

    /// <summary>
    /// Gets/sets the font size. Null if the style does not change the font style.
    /// </summary>
    public int? FontSize
    {
        get => fontSize;
        set
        {
            if (frozen)
            {
                throw new InvalidOperationException();
            }

            fontSize = value;
        }
    }

    /// <summary>
    /// Gets/sets the font weight. Null if the style does not change the font weight.
    /// </summary>
    public FontWeight? FontWeight
    {
        get => fontWeight;
        set
        {
            if (frozen)
            {
                throw new InvalidOperationException();
            }

            fontWeight = value;
        }
    }

    /// <summary>
    /// Gets/sets the font style. Null if the style does not change the font style.
    /// </summary>
    public FontStyle? FontStyle
    {
        get => fontStyle;
        set
        {
            if (frozen)
            {
                throw new InvalidOperationException();
            }

            fontStyle = value;
        }
    }

    /// <summary>
    ///  Gets/sets the underline flag. Null if the underline status does not change the font style.
    /// </summary>
    public bool? Underline
    {
        get => underline;
        set
        {
            if (frozen)
            {
                throw new InvalidOperationException();
            }

            underline = value;
        }
    }

    /// <summary>
    ///  Gets/sets the strikethrough flag. Null if the strikethrough status does not change the font style.
    /// </summary>
    public bool? Strikethrough
    {
        get => strikethrough;
        set
        {
            if (frozen)
            {
                throw new InvalidOperationException();
            }

            strikethrough = value;
        }
    }

    /// <summary>
    /// Gets/sets the foreground color applied by the style.
    /// </summary>
    public Brush Foreground
    {
        get => foreground!;
        set
        {
            if (frozen)
            {
                throw new InvalidOperationException();
            }

            foreground = value;
        }
    }

    /// <summary>
    /// Gets/sets the background color applied by the style.
    /// </summary>
    public Brush Background
    {
        get => background!;
        set
        {
            if (frozen)
            {
                throw new InvalidOperationException();
            }

            background = value;
        }
    }

    /// <summary>
    /// Whether no property has been initialized.
    /// </summary>
    internal bool IsEmpty =>
        fontWeight is null &&
        fontStyle is null &&
        underline is null &&
        strikethrough is null &&
        foreground is null &&
        background is null &&
        fontFamily is null &&
        fontSize is null;
    #endregion

    #region constructors
    /// <summary>
    /// Creates a new instance.
    /// </summary>
    public Style()
    {
    }
    #endregion

    #region methods
    /// <summary>
    /// Gets CSS code for the color.
    /// </summary>
    public virtual string ToCss()
    {
        var b = new StringBuilder();

        if (Foreground is not null)
        {
            var c = Foreground.GetColor(null!);

            if (c is not null)
            {
                b.AppendFormat(CultureInfo.InvariantCulture, $"color: #{c.Value.R:x2}{c.Value.G:x2}{c.Value.B:x2}; ");
            }
        }

        if (Background is not null)
        {
            var c = Background.GetColor(null!);
            
            if (c is not null)
            {
                b.AppendFormat(CultureInfo.InvariantCulture, $"background-color: #{c.Value.R:x2}{c.Value.G:x2}{c.Value.B:x2}; ");
            }
        }

        if (FontWeight is not null)
        {
            b.Append($"font-weight: {FontWeight.Value.ToString().ToLowerInvariant()}; ");
        }
        
        if (FontStyle is not null)
        {
            b.Append($"font-style: {FontStyle.Value.ToString().ToLowerInvariant()}; ");
        }

        if (Underline is not null)
        {
            b.Append($"border-bottom: {(Underline.Value ? "solid" : "none")}; ");
        }

        if (Strikethrough is not null)
        {
            b.Append($"text-decoration: {(Strikethrough.Value ? " line-through" : " none")}; ");
        }
        
        return b.ToString();
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return $"[{GetType().Name} {(string.IsNullOrEmpty(Name) ? ToCss() : Name)}]";
    }

    /// <summary>
    /// Prevent further changes to this instance.
    /// </summary>
    public virtual void Freeze()
    {
        frozen = true;
    }

    /// <summary>
    /// Gets whether this instance is frozen.
    /// </summary>
    public bool IsFrozen => frozen;

    /// <summary>
    /// Returns a thawed clone of this instance.
    /// </summary>
    public virtual Style Clone()
    {
        var clone = (Style)MemberwiseClone();
        
        clone.frozen = false;
        
        return clone;
    }

    object ICloneable.Clone()
    {
        return Clone();
    }

    /// <inheritdoc/>
    public override sealed bool Equals(object? obj)
    {
        return Equals(obj as Style);
    }

    /// <inheritdoc/>
    public virtual bool Equals(Style? other)
    {
        if (other is null)
        {
            return false;
        }

        return name == other.name && fontWeight == other.fontWeight
            && fontStyle == other.fontStyle && underline == other.underline && strikethrough == other.strikethrough
            && Equals(foreground, other.foreground) && Equals(background, other.background)
            && Equals(fontFamily, other.fontFamily) && Equals(FontSize, other.FontSize);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        int hashCode = 0;

        unchecked
        {
            if (name is not null)
            {
                hashCode += 1000000007 * name.GetHashCode();
            }

            hashCode += 1000000009 * fontWeight.GetHashCode();
            hashCode += 1000000021 * fontStyle.GetHashCode();
            
            if (foreground is not null)
            {
                hashCode += 1000000033 * foreground.GetHashCode();
            }

            if (background is not null)
            {
                hashCode += 1000000087 * background.GetHashCode();
            }

            if (fontFamily is not null)
            {
                hashCode += 1000000123 * fontFamily.GetHashCode();
            }

            if (fontSize is not null)
            {
                hashCode += 1000000167 * fontSize.GetHashCode();
            }
        }
        return hashCode;
    }

    /// <summary>
    /// Merge the given style non-null props with this style.
    /// </summary>
    public void Merge(Style style)
    {
        this.ThrowIfFrozen();

        if (style.IsEmpty)
        {
            return;
        }
        
        fontWeight = style.fontWeight ?? fontWeight;
        fontStyle = style.fontStyle ?? fontStyle;
        foreground = style.foreground ?? foreground;
        background = style.background ?? background;
        underline = style.underline ?? underline;
        strikethrough = style.strikethrough ?? strikethrough;
        fontFamily = style.fontFamily ?? fontFamily;
        fontSize = style.fontSize ?? fontSize;
    }
    #endregion
}
