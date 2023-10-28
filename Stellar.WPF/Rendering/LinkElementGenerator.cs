using System;
using System.Text.RegularExpressions;

namespace Stellar.WPF.Rendering;

/// <summary>
/// Detects hyperlinks and makes them clickable.
/// </summary>
/// <remarks>
/// This element generator can be easily enabled and configured using the
/// <see cref="TextEditorOptions"/>.
/// </remarks>
public partial class LinkElementGenerator : VisualLineGenerator, IElementGenerator
{
    // protocol (or www), followed by 0 or more 'link characters', followed by a link end character
    // (this allows accepting punctuation inside links but not at the end)
    [GeneratedRegex("\\b(https?://|ftp://|www\\.)[\\w\\d\\._/\\-~%@()+:?&=#!]*[\\w\\d/]")]
    private static partial Regex LinkRegex();
    
    internal static Regex linkRegex = LinkRegex();

    /// <summary>
    /// Gets/Sets whether the user needs to press Control to click the link.
    /// The default value is true.
    /// </summary>
    public bool RequireControlClick { get; set; }

    /// <summary>
    /// Creates a new LinkElementGenerator.
    /// </summary>
    public LinkElementGenerator()
    {
        RequireControlClick = true;
    }


    /// <summary>
    /// Creates a new LinkElementGenerator using the specified regex.
    /// </summary>
    protected LinkElementGenerator(Regex regex) : this()
    {
        linkRegex = regex;
    }

    void IElementGenerator.FetchOptions(TextEditorOptions options)
    {
        RequireControlClick = options.RequireControlClick;
    }

    Match GetMatch(int startOffset, out int offset)
    {
        var endOffset = Context!.VisualLine.LastLine.EndOffset;
        var relevantText = Context.GetText(startOffset, endOffset - startOffset);

        var match = linkRegex.Match(relevantText.Text, relevantText.Offset, relevantText.Count);

        offset = match.Success
            ? match.Index - relevantText.Offset + startOffset
            : -1;

        return match;
    }

    /// <inheritdoc/>
    public override int GetFirstInterestedOffset(int start)
    {
        GetMatch(start, out int offset);

        return offset;
    }

    /// <inheritdoc/>
    public override VisualLineElement ConstructElement(int start)
    {
        var match = GetMatch(start, out int offset);

        if (match.Success && offset == start)
        {
            return ConstructElementFromMatch(match);
        }

        return null!;
    }

    /// <summary>
    /// Constructs a VisualLineElement that replaces the matched text.
    /// The default implementation will create a <see cref="VisualLineLinkText"/>
    /// based on the URI provided by <see cref="GetUriFromMatch"/>.
    /// </summary>
    protected virtual VisualLineElement ConstructElementFromMatch(Match match)
    {
        var uri = GetUriFromMatch(match);

        if (uri is null)
        {
            return null!;
        }

        var linkText = new VisualLineLinkText(Context!.VisualLine, match.Length)
        {
            NavigateUri = uri,
            RequireControlClick = RequireControlClick
        };

        return linkText;
    }

    /// <summary>
    /// Fetches the URI from the regex match. Returns null if the URI format is invalid.
    /// </summary>
    protected virtual Uri GetUriFromMatch(Match match)
    {
        var targetUrl = Regex.Replace(match.Value, @"^www\.", "http://", RegexOptions.IgnoreCase);

        if (Uri.IsWellFormedUriString(targetUrl, UriKind.Absolute))
        {
            return new Uri(targetUrl);
        }

        return null!;
    }
}
