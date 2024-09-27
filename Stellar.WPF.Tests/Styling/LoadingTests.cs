using Stellar.WPF.Styling.IO;

namespace Stellar.WPF.Tests.Styling;

public class LoadingTests
{
    /// <summary>
    /// Loads a Syntax DTO.
    /// </summary>
    /// <remarks>
    /// Anchoring styles to keep code DRY is not compatible with
    /// ignoring the styles attribute: it mut be declared in the syntax DTO.
    /// That doesn't mean it should exist in the syntax object though. A
    /// memmory optimization argument could be made for object references on
    /// rules like comments, but styles are fairly lightweight, so best to
    /// let YAML.net instance on each rule and our code to not use them at all
    /// when instancing syntaxes.
    /// </remarks>
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
    fontStyle: Oblique
ruleSets:
  - rules:
    - keywords: true false
      style: *custom";

        var dto = Loader.Load(input);

        Assert.NotNull(dto);
        Assert.Equal("C#", dto.Name);

        var style0 = dto.Styles[0];
        var style1 = dto.Styles[1];
        var style2 = dto.Styles[2];

        Assert.Equal("comment", style0.Name);
        Assert.Equal("Green", style0.Foreground);

        Assert.Equal("extraLight", style1.FontWeight);
        Assert.Equal("Italic", style1.FontStyle);

        // validate references
        var styleR = dto.RuleSets[0].Rules[0].Style;

        Assert.Equal(style2.Name, styleR.Name);
        Assert.Equal(style2.FontStyle, styleR.FontStyle);

        //var syntax = new Syntax(dto);

        //Assert.Equal(System.Windows.FontStyles.Italic, syntax.Styles[1].FontStyle);
    }

    [Fact]
    public void LoadsFromResource()
    {
        var syntax = Loader.LoadFromResource("JavaScript");

        Assert.NotNull(syntax);
        //Assert.Equal("intrinsics", syntax.Styles[1].Name);
        Assert.Equal("TODO FIXME", syntax.RuleSets[2]?.Rules?[3].RuleSets?[0].Rules?[0].Keywords);
        Assert.Equal("XmlDoc.Comments", syntax.RuleSets[2]?.Rules?[2].RuleSets?[0].Imports);

    }
}
