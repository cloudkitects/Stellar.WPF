using System;
using System.Text.RegularExpressions;

namespace Stellar.WPF.Rendering;

/// <summary>
/// Detects e-mail addresses and makes them clickable.
/// </summary>
/// <remarks>
/// This element generator can be easily enabled and configured using the
/// <see cref="TextEditorOptions"/>.
/// </remarks>
sealed partial class MailElementGenerator : LinkElementGenerator
{
    [GeneratedRegex("\\b[\\w\\d\\.\\-]+\\@[\\w\\d\\.\\-]+\\.[a-z]{2,6}\\b")]
    private static partial Regex MailRegex();
    
    internal static Regex mailRegex = MailRegex();

    /// <summary>
    /// Creates a new MailElementGenerator.
    /// </summary>
    public MailElementGenerator() : base()
    {
    }

    /// <summary>
    /// Creates a new MailElementGenerator.
    /// </summary>
    public MailElementGenerator(Regex regex)
    {
        mailRegex = regex;
    }

    protected override Uri GetUriFromMatch(Match match)
    {
        var targetUrl = "mailto:" + match.Value;

        if (Uri.IsWellFormedUriString(targetUrl, UriKind.Absolute))
        {
            return new Uri(targetUrl);
        }

        return null!;
    }
}
