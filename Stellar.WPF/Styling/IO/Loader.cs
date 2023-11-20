using YamlDotNet.Serialization.NamingConventions;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NodeDeserializers;

namespace Stellar.WPF.Styling.IO;

public static class Loader
{
    public static SyntaxDto Load(string input)
    {
        var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();

        //var deserializer = new DeserializerBuilder()
        //    .WithNamingConvention(CamelCaseNamingConvention.Instance)
        //    .WithNodeDeserializer(inner => new AnchorNameDeserializer(inner), s => s.InsteadOf<ObjectNodeDeserializer>())
        //    .Build();

        return deserializer.Deserialize<SyntaxDto>(input);
    }
}
