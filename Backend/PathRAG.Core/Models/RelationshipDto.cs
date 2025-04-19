namespace PathRAG.Core.Models;

public class RelationshipDto
{
    public Guid Id { get; set; }
    public string SourceEntityId { get; set; } = string.Empty;
    public string TargetEntityId { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public float Weight { get; set; }
    public Guid VectorStoreId { get; set; }
    public DateTime CreatedAt { get; set; }
}
