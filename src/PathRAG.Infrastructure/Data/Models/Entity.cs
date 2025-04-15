namespace PathRAG.Core.Models;

public class GraphEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public float[] Embedding { get; set; } = Array.Empty<float>();
    public List<string> Keywords { get; set; } = new();
    public float Weight { get; set; }
    public string SourceId { get; set; }
    public DateTime CreatedAt { get; set; }
}