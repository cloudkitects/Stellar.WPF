using System;
using System.Globalization;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Windows;
using System.Windows.Media;

using Stellar.WPF.Utilities;

namespace Stellar.WPF.Highlighting;

/// <summary>
/// A set of font properties and foreground and background color.
/// </summary>
[Serializable]
public class Style : ISerializable, IFreezable, ICloneable, IEquatable<Style>
{
    #region fields and props
    internal static readonly Style Empty = new Style().Freeze<Style>();

    string name = string.Empty;
    FontFamily? fontFamily = null;
    int? fontSize;
    FontWeight? fontWeight;
    FontStyle? fontStyle;
    bool? underline;
    bool? strikethrough;
    Brush? foreground;
    Brush? background;
    bool frozen;

    /// <summary>
    /// Gets/Sets the name of the color.
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

    /// <summary>
    /// Deserializes an instance.
    /// </summary>
    protected Style(SerializationInfo info, StreamingContext context)
    {
        if (info == null)
        {
            throw new ArgumentNullException(nameof(info));
        }

        Name = info.GetString("Name")!;

        if (info.GetBoolean("HasWeight"))
        {
            FontWeight = System.Windows.FontWeight.FromOpenTypeWeight(info.GetInt32("Weight"));
        }

        if (info.GetBoolean("HasStyle"))
        {
            FontStyle = (FontStyle?)new FontStyleConverter().ConvertFromInvariantString(info.GetString("Style")!);
        }

        if (info.GetBoolean("HasUnderline"))
        {
            Underline = info.GetBoolean("Underline");
        }

        if (info.GetBoolean("HasStrikethrough"))
        {
            Strikethrough = info.GetBoolean("Strikethrough");
        }

        Foreground = (Brush)info.GetValue("Foreground", typeof(SimpleBrush))!;
        Background = (Brush)info.GetValue("Background", typeof(SimpleBrush))!;

        if (info.GetBoolean("HasFamily"))
        {
            FontFamily = new FontFamily(info.GetString("Family"));
        }

        if (info.GetBoolean("HasSize"))
        {
            FontSize = info.GetInt32("Size");
        }
    }
    #endregion

    #region methods
    /// <summary>
    /// Serializes this instance.
    /// </summary>
    [System.Security.SecurityCritical]
    public virtual void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        if (info == null)
        {
            throw new ArgumentNullException(nameof(info));
        }

        info.AddValue("Name", Name);
        info.AddValue("HasWeight", FontWeight.HasValue);

        if (FontWeight.HasValue)
        {
            info.AddValue("Weight", FontWeight.Value.ToOpenTypeWeight());
        }

        info.AddValue("HasStyle", FontStyle.HasValue);
        
        if (FontStyle.HasValue)
        {
            info.AddValue("Style", FontStyle.Value.ToString());
        }

        info.AddValue("HasUnderline", Underline.HasValue);
        
        if (Underline.HasValue)
        {
            info.AddValue("Underline", Underline.Value);
        }

        info.AddValue("HasStrikethrough", Strikethrough.HasValue);
        
        if (Strikethrough.HasValue)
        {
            info.AddValue("Strikethrough", Strikethrough.Value);
        }

        info.AddValue("Foreground", Foreground);
        info.AddValue("Background", Background);
        info.AddValue("HasFamily", FontFamily is not null);
        
        if (FontFamily is not null)
        {
            info.AddValue("Family", FontFamily.FamilyNames.FirstOrDefault());
        }

        info.AddValue("HasSize", FontSize.HasValue);

        if (FontSize.HasValue)
        {
            info.AddValue("Size", FontSize.Value.ToString());
        }
    }

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
    /// Clones this instance (and unfreezes the clone).
    /// </summary>
    public virtual Style Clone()
    {
        var c = (Style)MemberwiseClone();
        
        c.frozen = false;
        
        return c;
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
