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
        Assert.Equal("intrinsics", syntax.Styles[1].Name);
        Assert.Equal("TODO FIXME", syntax.RuleSets[1]?.Rules?[3].RuleSet?.Rules?[0].Keywords);
    }
}
