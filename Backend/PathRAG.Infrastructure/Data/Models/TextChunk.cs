using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PathRAG.Core.Models;

public class TextChunk
{
    [Key]
    public Guid Id { get; set; }
    
    [Required]
    public string Content { get; set; } = string.Empty;
    
    public float[] Embedding { get; set; } = Array.Empty<float>();
    
    public int TokenCount { get; set; }
    
    public string FullDocumentId { get; set; } = string.Empty;
    
    public int ChunkOrderIndex { get; set; }
    
    [Required]
    public Guid VectorStoreId { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    [ForeignKey("VectorStoreId")]
    public virtual VectorStore VectorStore { get; set; } = null!;
}
