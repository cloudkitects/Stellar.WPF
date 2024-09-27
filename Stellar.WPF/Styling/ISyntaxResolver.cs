namespace Stellar.WPF.Styling;

/// <summary>
/// Interface for resolving cross-referencing syntaxes.
/// </summary>
public interface ISyntaxResolver
{
    /// <summary>
    /// Get a syntax by name.
    /// </summary>
    ISyntax? GetSyntax(string name);
}
