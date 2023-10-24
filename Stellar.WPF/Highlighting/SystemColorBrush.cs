using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.Serialization;
using System.Windows;

using Stellar.WPF.Rendering;

namespace Stellar.WPF.Highlighting;

/// <summary>
/// HighlightingBrush implementation that finds a brush using a resource.
/// </summary>
[Serializable]
sealed class SystemColorBrush : Brush, ISerializable
{
	readonly PropertyInfo property;

	public SystemColorBrush(PropertyInfo property)
	{
		Debug.Assert(property.ReflectedType == typeof(SystemColors));
        Debug.Assert(typeof(System.Windows.Media.Brush).IsAssignableFrom(property.PropertyType));
		this.property = property;
	}

	public override System.Windows.Media.Brush GetBrush(ITextRunContext context)
	{
		return (System.Windows.Media.Brush)property.GetValue(null, null)!;
	}

	public override string ToString()
	{
		return property.Name;
	}

	SystemColorBrush(SerializationInfo info, StreamingContext context)
    {
        property = typeof(SystemColors).GetProperty(info.GetString("propertyName")!)!;
		
		if (property is null)
        {
            throw new ArgumentException($"Error deserializing {GetType().Name}");
        }
    }

	void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
	{
		info.AddValue("propertyName", property.Name);
	}

	public override bool Equals(object? obj)
	{
        return obj is SystemColorBrush other && Equals(property, other.property);
    }

    public override int GetHashCode()
	{
		return property.GetHashCode();
	}
}
