using System.Text.Json.Serialization;

namespace AskData.KernelMemory.Graph;

internal class GraphDocument
{
    [JsonPropertyName("nodes")]
    public List<Node> Nodes { get; set; } = [];

    [JsonPropertyName("relationships")]
    public List<Relationship> Relationships { get; set; } = [];

    public class Node
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("title")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("properties")]
        public Dictionary<string, object> Properties { get; set; } = [];
    }

    public class Relationship
    {
        [JsonPropertyName("source")]
        public Node? Source { get; set; }

        [JsonPropertyName("target")]
        public Node? Target { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("properties")]
        public Dictionary<string, object> Properties { get; set; } = [];
    }
}
