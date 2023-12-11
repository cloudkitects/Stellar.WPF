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
    fontStyle: Italic
  - &custom
    fontWeight: 250
    fontStyle: Oblique";

        var dto = Loader.Load(input);

        Assert.NotNull(dto);

        Assert.Equal("comment", dto.Styles[0].Name);
        Assert.Equal("C#", dto.Name);
        Assert.Equal("Green", dto.Styles[0].Foreground!);
        
        Assert.Equal("allprops", dto.Styles[1].Name);
        Assert.Equal("extraLight", dto.Styles[1].FontWeight!);
        Assert.Equal("Italic", dto.Styles[1].FontStyle!);

        //var syntax = new Syntax(dto);
        
        //Assert.Equal(System.Windows.FontStyles.Italic, syntax.Styles[1].FontStyle);
    }

    [Fact]
    public void LoadsFromResource()
    {
        var syntax = Loader.LoadFromResource("JavaScript");

        Assert.NotNull(syntax);
        Assert.Equal("intrinsics", syntax.Styles[1].Name);
        Assert.Equal("TODO FIXME", syntax.RuleSets[2]?.Rules?[3].RuleSets?[0].Rules?[0].Keywords);
        Assert.Equal("XmlDoc.Comments", syntax.RuleSets[2]?.Rules?[2].RuleSets?[0].Import);

    }
}
