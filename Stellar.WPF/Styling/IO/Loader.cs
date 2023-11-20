using YamlDotNet.Serialization.NamingConventions;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NodeDeserializers;

namespace Stellar.WPF.Styling.IO;

public static class Loader
{
    public static SyntaxDto Load(string input)
    {
        //var deserializer = new DeserializerBuilder()
        //        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        //        .Build();

        var valueDeserializer = new DeserializerBuilder()
                    .WithNamingConvention(CamelCaseNamingConvention.Instance)
                    .BuildValueDeserializer();

        var deserializer = Deserializer.FromValueDeserializer(new AnchorNameDeserializer(valueDeserializer));

        return deserializer.Deserialize<SyntaxDto>(input);
    }
}
