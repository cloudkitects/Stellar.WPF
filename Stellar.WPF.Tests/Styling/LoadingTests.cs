namespace Stellar.WPF.Tests.Styling;

public class LoadingTests
{
    [Fact]
    public void Loads()
    {
        var input = @"---
Name: C#
Extensions: .cs,cshtml
Styles:
  - Name: comment
    foreground: Green";

        var syntax = WPF.Styling.IO.Loader.Load(input);

        Assert.NotNull(syntax);
        Assert.Equal("C#", syntax.Name);
        Assert.Equal("C#", syntax.Styles[0].Foreground.ToString());

    }
}
