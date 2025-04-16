using System.Text.Json.Serialization;

namespace PathRAG.Core.Models;

public class ExtractionData
{
    [JsonPropertyName("entities")]
    public List<EntityData> Entities { get; set; } = new List<EntityData>();

    [JsonPropertyName("relationships")]
    public List<RelationshipData> Relationships { get; set; } = new List<RelationshipData>();
}

public class EntityData
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("keywords")]
    public List<string>? Keywords { get; set; }
}

public class RelationshipData
{
    [JsonPropertyName("source")]
    public string Source { get; set; } = string.Empty;

    [JsonPropertyName("target")]
    public string Target { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("keywords")]
    public List<string>? Keywords { get; set; }
}
