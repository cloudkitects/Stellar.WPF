using Stellar.WPF.Styling.IO;

namespace Stellar.WPF.Tests.Styling;

public class LoadingTests
{
    [Fact]
    public void Loads()
    {
        var input = @"---
name: C#
extensions: [cs,cshtml]
styles:
  - &comment
    foreground: Green
  - &allprops
    fontWeight: extraLight
    fontStyle: italic
  - &custom
    fontWeight: 250
    fontStyle: bold";

        var syntax = Loader.Load(input);

        Assert.NotNull(syntax);

        Assert.Equal("comment", syntax.Styles[0].Name);
        Assert.Equal("C#", syntax.Name);
        Assert.Equal("Green", syntax.Styles[0].Foreground!);
        
        Assert.Equal("allprops", syntax.Styles[1].Name);
        Assert.Equal("extraLight", syntax.Styles[1].FontWeight!);
        Assert.Equal("italic", syntax.Styles[1].FontStyle!);

        // DTO to O

    }

    [Fact]
    public void LoadsFromResource()
    {
        var syntax = Loader.LoadFromResource("JavaScript");

        Assert.NotNull(syntax);
    }

    [Fact]
    public void Write()
    {
        var syntax = new SyntaxDto()
        {
            Name = "JavaScript",
            Extensions = new[] { "js" },
            Styles = new List<StyleDto>
            {
                new StyleDto { Name = "dummy", Foreground = "Blue" }
            },
            RuleSets = new List<RuleSetDto>
            {
                new RuleSetDto
                {
                    Name = "CommentMarkerSet",
                    Elements = new List<ElementDto>
                    {
                        new ElementDto
                        {
                            Keywords = "TODO FIXME",
                            Style = new StyleDto { Name = "dummy2", Background = "Yellow" }
                        }
                    }
                },
                new RuleSetDto
                {
                    Name = "main",
                    Elements = new List<ElementDto>
                    {
                        new ElementDto
                        {
                            Span = "/*",
                            End = "*/",
                            Multiline = true,
                            Style = new StyleDto { Foreground = "Red" }
                        }
                    }
                }
            }
        };

        var yaml = Loader.Save(syntax);

        Assert.NotNull(yaml);
    }
}
