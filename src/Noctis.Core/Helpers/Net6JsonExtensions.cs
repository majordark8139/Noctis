#if !NET8_0_OR_GREATER
namespace System.Text.Json.Nodes;

public static class Net6JsonExtensions
{
    public static JsonNode DeepClone(this JsonNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return JsonNode.Parse(node.ToJsonString())!;
    }
}
#endif
