using System;

using YamlDotNet.Core.Events;
using YamlDotNet.Core;
using YamlDotNet.Serialization.Utilities;
using YamlDotNet.Serialization;

namespace Stellar.WPF.Styling.IO;

public interface IAnchoredObject
{
    string? Name { get; set; }
}

public class AnchorNameDeserializer : IValueDeserializer
{
    private readonly IValueDeserializer innerValueDeserializer;

    public AnchorNameDeserializer(IValueDeserializer innerValueDeserializer)
    {
        this.innerValueDeserializer = innerValueDeserializer;
    }

    public object DeserializeValue(IParser parser, Type expectedType, SerializerState state, IValueDeserializer nestedObjectDeserializer)
    {
        string? name = null;
        
        if (parser.Current is NodeEvent nodeEvent && !nodeEvent.Anchor.IsEmpty)
        {
            name = nodeEvent.Anchor.Value;
        }
        
        var result = innerValueDeserializer.DeserializeValue(parser, expectedType, state, nestedObjectDeserializer);
        
        if (name is not null)
        {
            if (result is IAnchoredObject anchored)
            {
                anchored.Name = name;
            }
        }

        return result!;
    }
}

