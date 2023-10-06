namespace Stellar.WPF.Document;

/// <summary>
/// Defines how a text anchor moves.
/// </summary>
public enum AnchorMovementType
{
    /// <summary>
    /// When text is inserted at the anchor position, the type of the insertion
    /// determines where the caret moves to. For normal insertions, the anchor will move
    /// after the inserted text.
    /// </summary>
    Default,
    /// <summary>
    /// Behaves like a start marker - when text is inserted at the anchor position, the anchor will stay
    /// before the inserted text.
    /// </summary>
    BeforeInsertion,
    /// <summary>
    /// Behave like an end marker - when text is inserted at the anchor position, the anchor will move
    /// after the inserted text.
    /// </summary>
    AfterInsertion
}
