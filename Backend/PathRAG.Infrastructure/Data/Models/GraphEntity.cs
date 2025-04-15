using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PathRAG.Core.Models;

public class GraphEntity
{
    [Key]
    public Guid Id { get; set; }
    
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(50)]
    public string Type { get; set; } = string.Empty;
    
    public string Description { get; set; } = string.Empty;
    
    public float[] Embedding { get; set; } = Array.Empty<float>();
    
    public List<string> Keywords { get; set; } = new();
    
    public float Weight { get; set; }
    
    public string SourceId { get; set; } = string.Empty;
    
    [Required]
    public Guid VectorStoreId { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    [ForeignKey("VectorStoreId")]
    public virtual VectorStore VectorStore { get; set; } = null!;
}
