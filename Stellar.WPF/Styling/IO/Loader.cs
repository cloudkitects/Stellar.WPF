using YamlDotNet.Serialization.NamingConventions;
using YamlDotNet.Serialization;
using System;

namespace Stellar.WPF.Styling.IO;

public static class Loader
{
    public static SyntaxDto Load(string input)
    {
        var valueDeserializer = new DeserializerBuilder()
                    .WithNamingConvention(CamelCaseNamingConvention.Instance)
                    .BuildValueDeserializer();

        var deserializer = Deserializer.FromValueDeserializer(new AnchorNameDeserializer(valueDeserializer));

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
}
