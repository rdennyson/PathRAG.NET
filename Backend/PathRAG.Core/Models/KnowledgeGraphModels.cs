using System.Text.Json.Serialization;

namespace PathRAG.Core.Models;

public class KnowledgeGraphNode
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("stroke")]
    public string Stroke { get; set; } = "#000000";

    [JsonPropertyName("background")]
    public string Background { get; set; } = "#FFFFFF";

    [JsonPropertyName("x")]
    public int X { get; set; }

    [JsonPropertyName("y")]
    public int Y { get; set; }

    [JsonPropertyName("adjacencies")]
    public List<KnowledgeGraphEdge> Adjacencies { get; set; } = new List<KnowledgeGraphEdge>();
}

public class KnowledgeGraphEdge
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("source")]
    public string Source { get; set; } = string.Empty;

    [JsonPropertyName("target")]
    public string Target { get; set; } = string.Empty;

    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("color")]
    public string Color { get; set; } = "#999999";
}
