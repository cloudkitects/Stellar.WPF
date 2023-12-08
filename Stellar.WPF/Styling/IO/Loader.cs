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

        //var deserializer = new DeserializerBuilder()
        //            .WithNamingConvention(CamelCaseNamingConvention.Instance)
        //            .Build();

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

    public static string Save(SyntaxDto syntax)
    {
        var serializer = new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        return serializer.Serialize(syntax);
    }
}
