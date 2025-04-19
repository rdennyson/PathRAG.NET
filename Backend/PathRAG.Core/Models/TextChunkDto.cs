namespace PathRAG.Core.Models;

public class TextChunkDto
{
    public Guid Id { get; set; }
    public string Content { get; set; } = string.Empty;
    public Guid DocumentId { get; set; }
    public Guid VectorStoreId { get; set; }
    public DateTime CreatedAt { get; set; }
}
