namespace Stellar.WPF.Rendering;

/// <summary>
/// An enumeration of well-known layers.
/// </summary>
public enum KnownLayer
{
    /// <summary>
    /// Background, directly drawn in the TextView. No UIElement represents this layer, 
    /// and no layer can replace it or be inserted below it.
    /// </summary>
    /// <remarks>It is below the Selection layer.</remarks>
    Background,
    /// <summary>
    /// The selection rectangle.
    /// </summary>
    /// <remarks>Sits between the Background and the Text layers.</remarks>
    Selection,
    /// <summary>
    /// The text and inline UI elements.
    /// </summary>
    /// <remarks>Sits between the Selection and the Caret layers.</remarks>
    Text,
    /// <summary>
    /// The blinking caret.
    /// </summary>
    /// <remarks>Always above the Text layer.</remarks>
    Caret
}

/// <summary>
/// Specifies where a new layer is inserted in relation to an existing layer.
/// </summary>
public enum LayerInsertionPosition
{
    /// <summary>
    /// Below the specified layer.
    /// </summary>
    Below,
    /// <summary>
    /// Replaces the specified layer--removes the existing layer
    /// from the TextView.Layers" collection.
    /// </summary>
    Replace,
    /// <summary>
    /// Above the specified layer.
    /// </summary>
    Above
}