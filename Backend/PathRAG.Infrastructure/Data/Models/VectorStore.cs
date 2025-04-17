using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PathRAG.Infrastructure.Models;
[Table("vectorstore", Schema = "public")]
public class VectorStore
{
    [Key]
    public Guid Id { get; set; }
    
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    
    public string UserId { get; set; } = string.Empty;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public virtual ICollection<TextChunk> TextChunks { get; set; } = new List<TextChunk>();
    
    public virtual ICollection<GraphEntity> Entities { get; set; } = new List<GraphEntity>();
    
    public virtual ICollection<Relationship> Relationships { get; set; } = new List<Relationship>();
    
    public virtual ICollection<AssistantVectorStore> AssistantVectorStores { get; set; } = new List<AssistantVectorStore>();
}
