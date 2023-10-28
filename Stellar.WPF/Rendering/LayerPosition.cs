using System;
using System.Windows;

namespace Stellar.WPF.Rendering;

/// <summary>
/// A composite keeping track of UI elements' layer/insertion position.
/// </summary>
sealed class LayerPosition : IComparable<LayerPosition>
{
    internal readonly KnownLayer KnownLayer;
    internal readonly LayerInsertionPosition Position;
    
    internal static readonly DependencyProperty LayerPositionProperty =
        DependencyProperty.RegisterAttached("LayerPosition", typeof(LayerPosition), typeof(LayerPosition));

    /// <summary>
    /// Constructor
    /// </summary>
    public LayerPosition(KnownLayer knownLayer, LayerInsertionPosition position)
    {
        KnownLayer = knownLayer;
        Position = position;
    }

    /// <summary>
    /// Getter
    /// </summary>
    public static LayerPosition GetLayerPosition(UIElement layer)
    {
        return (LayerPosition)layer.GetValue(LayerPositionProperty);
    }

    /// <summary>
    /// Setter
    /// </summary>
    public static void SetLayerPosition(UIElement layer, LayerPosition value)
    {
        layer.SetValue(LayerPositionProperty, value);
    }

    /// <summary>
    /// Compare two layer positions drilling down if in the same layer.
    /// </summary>
    public int CompareTo(LayerPosition? other)
    {
        var result = KnownLayer.CompareTo(other?.KnownLayer);

        return result == 0
            ? Position.CompareTo(other?.Position)
            : result;
    }
}
