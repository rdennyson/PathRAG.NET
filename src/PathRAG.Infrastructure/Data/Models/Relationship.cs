namespace PathRAG.Core.Models;

public class Relationship
{
    public Guid Id { get; set; }
    public string SourceEntityId { get; set; }
    public string TargetEntityId { get; set; }
    public float Weight { get; set; }
    public List<string> Keywords { get; set; } = new();
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public float[] Embedding { get; set; } = Array.Empty<float>();
    public DateTime CreatedAt { get; set; }
}