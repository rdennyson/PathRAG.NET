namespace PathRAG.Core.Models;

public class GraphEntityDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> Keywords { get; set; } = new();
    public float Weight { get; set; }
    public Guid VectorStoreId { get; set; }
    public DateTime CreatedAt { get; set; }
}
