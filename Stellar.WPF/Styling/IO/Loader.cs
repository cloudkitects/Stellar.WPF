using YamlDotNet.Serialization.NamingConventions;
using YamlDotNet.Serialization;

namespace Stellar.WPF.Styling.IO;

public static class Loader
{
    public static Syntax Load(string input)
    {
        var deserializer = new DeserializerBuilder()
                .WithNamingConvention(PascalCaseNamingConvention.Instance)
                .Build();

        return deserializer.Deserialize<Syntax>(input);
    }
}
