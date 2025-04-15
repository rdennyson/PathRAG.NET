using System.Text.Json.Serialization;

namespace PathRAG.Core.Models;

public class TextChunk
{
    public Guid Id { get; set; }
    public string Content { get; set; } = string.Empty;
    public float[] Embedding { get; set; } = Array.Empty<float>();
    public int TokenCount { get; set; }
    public string FullDocumentId { get; set; } = string.Empty;
    public int ChunkOrderIndex { get; set; }
    public DateTime CreatedAt { get; set; }
}