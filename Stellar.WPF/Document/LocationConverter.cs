using System;
using System.ComponentModel;
using System.Globalization;

namespace Stellar.WPF.Document;

/// <summary>
/// Converts strings of the form '0+[;,]0+' to a <see cref="Location"/> and vice versa.
/// </summary>
public class LocationConverter : TypeConverter
{
    /// <inheritdoc/>
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) => sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

    /// <inheritdoc/>
    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType) => destinationType == typeof(Location) || base.CanConvertTo(context, destinationType);

    /// <inheritdoc/>
    public override object ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is string v)
        {
            string[] parts = v.Split(';', ',');

            if (parts.Length == 2)
            {
                return new Location(int.Parse(parts[0], culture), int.Parse(parts[1], culture));
            }
        }

        return base.ConvertFrom(context, culture, value) ?? new Location();
    }

    /// <inheritdoc/>
    public override object ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        if (value is Location location && destinationType == typeof(string))
        {
            var loc = location;

            return loc.Line.ToString(culture) + ";" + loc.Column.ToString(culture);
        }

        return base.ConvertTo(context, culture, value, destinationType) ?? "0;0";
    }
}
