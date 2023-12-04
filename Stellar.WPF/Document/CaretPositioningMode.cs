namespace Stellar.WPF.Document
{
    /// <summary>
    /// Specifies the mode for getting the next caret position.
    /// </summary>
    public enum CaretPositioningMode
    {
        /// <summary>
        /// Normal positioning, stop after every grapheme.
        /// </summary>
        Normal,
        /// <summary>
        /// Stop only on word borders.
        /// </summary>
        WordBorder,
        /// <summary>
        /// Stop only at the beginning of words. This is used for Ctrl+Left/Ctrl+Right.
        /// </summary>
        WordStart,
        /// <summary>
        /// Stop only at the beginning of words, and anywhere in the middle of symbols.
        /// </summary>
        WordStartOrSymbol,
        /// <summary>
        /// Stop only on word borders, and anywhere in the middle of symbols.
        /// </summary>
        WordBorderOrSymbol,
        /// <summary>
        /// Stop between every Unicode codepoint, even within the same grapheme.
        /// Used to implement deleting the previous grapheme when Backspace is pressed.
        /// </summary>
        EveryCodepoint
    }
}
