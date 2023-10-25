namespace Stellar.WPF.Document;

/// <summary>
/// Classifies a character.
/// </summary>
public enum CharacterClass
{
    /// <summary>
    /// None of the other classes.
    /// </summary>
    Other,
    /// <summary>
    /// Whitespace, except a line terminator.
    /// </summary>
    Whitespace,
    /// <summary>
    /// Part of an identifier (letter, digit or underscore).
    /// </summary>
    IdentifierPart,
    /// <summary>
    /// A line terminator (\r or \n).
    /// </summary>
    LineTerminator,
    /// <summary>
    /// A unicode combining mark that modifies the previous character.
    /// Corresponds to Unicode designations "Mn", "Mc" and "Me".
    /// </summary>
    CombiningMark
}
