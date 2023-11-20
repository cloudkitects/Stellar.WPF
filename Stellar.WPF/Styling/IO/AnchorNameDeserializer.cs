using System;

using YamlDotNet.Core.Events;
using YamlDotNet.Core;
using YamlDotNet.Serialization.Utilities;
using YamlDotNet.Serialization;

namespace Stellar.WPF.Styling.IO;

public interface IAnchoredObject
{
    string Name { get; set; }
}

public class AnchorNameDeserializer : INodeDeserializer
{
    private readonly INodeDeserializer _deserializer;

    public AnchorNameDeserializer(INodeDeserializer deserializer)
    {
        _deserializer = deserializer;
    }

    bool INodeDeserializer.Deserialize(IParser parser, Type expectedType, Func<IParser, Type, object?> nestedObjectDeserializer, out object? value)
    {
        var nodeEvent = parser?.Current as NodeEvent;

        if (nodeEvent is null || nodeEvent.Anchor.IsEmpty)
        {
            value = null;
            
            return false;
        }
        
        var anchor = nodeEvent?.Anchor.Value;

        bool success = _deserializer.Deserialize(parser!, expectedType, nestedObjectDeserializer, out var result);

        if (result is IAnchoredObject anchoredObject && anchor is not null)
        {
            anchoredObject.Name = anchor;
        }
        
        value = result;
        
        return success;
    }
}
