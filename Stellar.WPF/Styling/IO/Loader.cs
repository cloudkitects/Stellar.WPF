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
    public static SyntaxDto Load(string input)
    {
        var valueDeserializer = new DeserializerBuilder()
                    .WithNamingConvention(CamelCaseNamingConvention.Instance)
                    .BuildValueDeserializer();

        var deserializer = Deserializer.FromValueDeserializer(new AnchorNameDeserializer(valueDeserializer));

        //var deserializer = new DeserializerBuilder()
        //            .WithNamingConvention(CamelCaseNamingConvention.Instance)
        //            .Build();

        return deserializer.Deserialize<SyntaxDto>(input);
    }

    public static SyntaxDto LoadFromResource(string name)
    {

        return name switch
        {
            "JavaScript" => Load(Resources.JavaScript),
            _ => throw new ArgumentException("Resource not implemented", nameof(name))
        };
    }

    internal static Style StyleFromDto(StyleDto dto)
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

        if (dto.FontSize.HasValue)
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

        if (dto.Underline.HasValue)
        {
            style.Underline = dto.Underline;
        }

        if (dto.Strikethrough.HasValue)
        {
            style.Strikethrough = dto.Strikethrough;
        }

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

    internal static Rule RuleFromDto(RuleDto dto)
    {
        var rule = new Rule();

        var options = RegexOptions.Compiled |
            (dto.IgnoreCase == true ? RegexOptions.IgnoreCase : 0) |
            (dto.Multiline  == true ? RegexOptions.Multiline  : 0);

        if (dto.Keywords is not null)
        {
            rule.Regex = new Regex(BuildKeywordsRegex(dto.Keywords), options);
        }
        else if (dto.Rule is not null)
        {
            rule.Regex = new Regex(dto.Rule, options);
        }


        return rule;
    }

    internal static string BuildKeywordsRegex(string keywords)
    {
        // split keywords and sort them so that "int" is captured before "in"
        var words = Regex.Split(keywords, @"\s+")
            .OrderByDescending(w => w.Length)
            .ToArray();

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

    public static string Save(SyntaxDto syntax)
    {
        var serializer = new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        return serializer.Serialize(syntax);
    }
}
