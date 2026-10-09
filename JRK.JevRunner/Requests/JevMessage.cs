using System.Text.Json;
using System.Text.Json.Nodes;

namespace JRK.JevRunner.Requests;

/// <summary>
/// Represents text, an object array, or a single object used as request state or question instructions.
/// </summary>
public union JevMessage(string, object[],object)
{
    /// <summary>
    /// Serializes the stored content to a JSON value, array, or object for inclusion in a request.
    /// </summary>
    /// <exception cref="NotImplementedException">The stored value does not match a supported case.</exception>
    public JsonNode Node => Value switch
    {
        object[] array => JsonSerializer.SerializeToNode(array)!.AsArray(),
        string str => JsonSerializer.SerializeToNode(str)!,
        { } single => JsonSerializer.SerializeToNode(single)!,
        _ => throw new NotImplementedException()
    };

    /// <summary>
    /// Normalizes the stored content to an array so messages can be concatenated without losing individual items.
    /// </summary>
    private object[] Array => Value switch
    {
        string str => [str],
        object[] array => array,
        { } s => [s],
        _ => throw new NotImplementedException()
    };
    
    /// <summary>
    /// Creates a message containing this message's items followed by the supplied message's items.
    /// </summary>
    public JevMessage CombineWith(JevMessage message)
    {
        var items = Array.Concat(message.Array);
        return items.ToArray();
    }
}
