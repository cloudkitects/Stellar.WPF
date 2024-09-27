using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;

using YamlDotNet.Serialization.NamingConventions;
using YamlDotNet.Serialization;
using System.Linq;

namespace Stellar.WPF.Styling.IO;

public static class Loader
{
    #region deserialize
    /// <summary>
    /// Load a Syntax DTO from YAML.
    /// </summary>
    public static SyntaxDto Load(string yaml)
    {
        var valueDeserializer = new DeserializerBuilder()
                    .WithNamingConvention(CamelCaseNamingConvention.Instance)
                    .IgnoreUnmatchedProperties()
                    .BuildValueDeserializer();

        var deserializer = Deserializer.FromValueDeserializer(new AnchorNameDeserializer(valueDeserializer));

        return deserializer.Deserialize<SyntaxDto>(yaml);
    }

    /// <summary>
    /// Load a Syntax DTO from an embedded resource's YAML contents.
    /// </summary>
    public static SyntaxDto LoadFromResource(string name)
    {

        return name switch
        {
            "JavaScript" => Load(Resources.JavaScript),
            _ => throw new ArgumentException("Resource not implemented", nameof(name))
        };
    }
    #endregion

    #region Load from DTOs
    public static ISyntax Load(SyntaxDto dto, ISyntaxResolver resolver)
    {
        if (dto is null)
        {
            throw new ArgumentNullException(nameof(dto));
        }

        return new Syntax(dto, resolver);
    }

    internal static Style LoadStyle(StyleDto dto)
    {
        var style = new Style();

        if (dto is null)
        {
            throw new ArgumentNullException(nameof(dto));
        }

        style.Name = dto.Name!;

        if (!string.IsNullOrWhiteSpace(dto.FontFamily))
        {
            style.FontFamily = new FontFamily(dto.FontFamily);
        }

        if (dto.FontSize >= 0)
        {
            style.FontSize = dto.FontSize;
        }

        if (!string.IsNullOrWhiteSpace(dto.FontWeight))
        {
            if (int.TryParse(dto.FontWeight, out var result))
            {
                style.FontWeight = FontWeight.FromOpenTypeWeight(result);
            }
            else
            {
                style.FontWeight = (FontWeight)new FontWeightConverter().ConvertFromString(dto.FontWeight)!;
            }
        }

        if (!string.IsNullOrWhiteSpace(dto.FontStyle))
        {
            style.FontStyle = (FontStyle)(new FontStyleConverter().ConvertFrom(dto.FontStyle))!;
        }

        style.Underline = dto.Underline;
        style.Strikethrough = dto.Strikethrough;

        if (!string.IsNullOrWhiteSpace(dto.Foreground))
        {
            style.Foreground = new SimpleBrush(dto.Foreground);
        }

        if (!string.IsNullOrWhiteSpace(dto.Background))
        {
            style.Background = new SimpleBrush(dto.Background);
        }

        return style;
    }

    internal static Context LoadRuleSet(ContextDto dto)
    {
        var ruleSet = new Context() { Name = dto.Name };

        foreach (var rule in dto.Rules!)
        {
            ruleSet.Rules.Add(LoadRule(rule));
        }

        return ruleSet;
    }

    /// <summary>
    /// Load a rule from a DTO.
    /// </summary>
    /// <remarks>
    /// At a minimum, a rule has one regex and a style.
    /// Ignore case and multiline regex options are optional.
    /// The DTO element name dictates whether to take the regex as is or build one.
    /// Also optional, rule sets for embedded snippets with different syntax.
    /// </remarks>
    internal static Rule LoadRule(RuleDto dto)
    {
        // check for required members
        if ((dto.Keywords is null && dto.Span is null && dto.Rule is null) || dto.Style is null)
        {
            throw new ArgumentException("keywords, span or rule and style attributes are required.", nameof(dto));
        }

        var rule = new Rule();

        var options = RegexOptions.Compiled |
            (dto.IgnoreCase == true ? RegexOptions.IgnoreCase : 0) |
            (dto.Multiline == true ? RegexOptions.Multiline : 0);

        if (dto.Keywords is not null)
        {
            rule.Regex = new Regex(BuildKeywordsRegex(dto.Keywords), options);
        }
        else if (dto.Span is not null)
        {
            rule.Regex = new Regex($"{dto.Span}.*{dto.Stop}", options);
        }
        else if (dto.Rule is not null)
        {
            rule.Regex = new Regex(dto.Rule, options);
        }

        // TODO: load rule sets!

        return rule;
    }

    internal static string BuildKeywordsRegex(string keywords)
    {
        // sort so that "int" is captured before "in"
#pragma warning disable SYSLIB1045 // Convert to 'GeneratedRegexAttribute'.
        var words = Regex.Split(keywords, @"\s+")
            .OrderByDescending(w => w.Length)
            .ToArray();
#pragma warning restore SYSLIB1045 // Convert to 'GeneratedRegexAttribute'.

        var regex = new StringBuilder();

        var simple = words.All(IsSimpleWord);

        var b = @"\b";
        var n = string.Empty;

        if (!simple)
        {
            // add the word boundary as needed
            words = words
                .Select(w => @$"{(char.IsLetterOrDigit(w[0]) ? b : n)}{w}{(char.IsLetterOrDigit(w[^1]) ? b : n)}")
                .ToArray();

            b = string.Empty;
        }

        // make the group atomic with ?> for improved performance
        regex.Append(@$"{b}(?>");
        regex.Append(Regex.Escape(string.Join('|', words)));
        regex.Append(@$"){b}");

        return regex.ToString();
    }

    /// <summary>
    /// Whether a word starts and ends with a letter or a digit, as opposed to "complex"
    /// words like -reserved, .maxstack or *bold*.
    /// </summary>
    static bool IsSimpleWord(string word)
    {
        return char.IsLetterOrDigit(word[0]) && char.IsLetterOrDigit(word[^1]);
    }
    #endregion
}
