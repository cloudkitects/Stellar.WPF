using Stellar.WPF.Styling.IO;

namespace Stellar.WPF.Tests.Styling;

public class LoadingTests
{
    [Fact]
    public void Loads()
    {
        var input = @"---
name: C#
extensions: [.cs,cshtml]
styles:
  - &comment
    foreground: Green
  - &allprops
    fontWeight: extraLight
    fontStyle: italic";

        var syntax = Loader.Load(input);

        Assert.NotNull(syntax);
        Assert.Equal("C#", syntax.Name);
        //Assert.Equal("comment", syntax.Styles[0].Name);
        Assert.Equal("Green", syntax.Styles[0].Foreground!);
        Assert.Equal("extraLight", syntax.Styles[1].FontWeight!);
        Assert.Equal("italic", syntax.Styles[1].FontStyle!);
    }
}
