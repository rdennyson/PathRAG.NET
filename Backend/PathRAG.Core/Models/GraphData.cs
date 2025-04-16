using System.Text.Json.Serialization;

namespace PathRAG.Core.Models;

/// <summary>
/// Represents the complete graph data for visualization
/// </summary>
public class GraphData
{
    /// <summary>
    /// List of entities (nodes) in the graph
    /// </summary>
    [JsonPropertyName("entities")]
    public List<GraphEntity> Entities { get; set; } = new List<GraphEntity>();

    /// <summary>
    /// List of relationships (edges) in the graph
    /// </summary>
    [JsonPropertyName("relationships")]
    public List<Relationship> Relationships { get; set; } = new List<Relationship>();
}
